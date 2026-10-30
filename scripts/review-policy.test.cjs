const assert = require('node:assert/strict');
const test = require('node:test');
const policy = require('../.github/review-policy.json');
const { chooseReviewers, assign } = require('../.github/scripts/assign-reviewers.cjs');

test('Members get one reviewer plus Team Lead; own Team Lead PR has no reviewer requests', () => {
  for (const author of [...Object.keys(policy.byAuthor), policy.lead]) {
    const actual = chooseReviewers(policy, author);
    assert.equal(actual.length, author === policy.lead ? 0 : 2);
    assert.ok(!actual.includes(author));
    if (author !== policy.lead) assert.ok(actual.includes(policy.lead));
  }
});

test('Issue reviewer is used and actual author is excluded even if they differ from owner', () => {
  assert.deepEqual(chooseReviewers(policy, 'Nickovlev', 'Refs #3'), ['VladimirKhmelev', policy.lead]);
  const changedAuthor = chooseReviewers(policy, 'VladimirKhmelev', 'Closes #3');
  assert.deepEqual(changedAuthor, ['Nickovlev', policy.lead]);
});

test('Author exclusion is case insensitive and unknown authors use a valid fallback', () => {
  assert.deepEqual(chooseReviewers(policy, 'ALKEN1T15'), []);
  assert.deepEqual(chooseReviewers(policy, 'contributor'), ['fUS1ONd', policy.lead]);
});

test('All issue mappings choose a non-lead reviewer and no candidate means failure', () => {
  for (const reviewer of Object.values(policy.byIssue)) assert.notEqual(reviewer, policy.lead);
  assert.throws(() => chooseReviewers({ lead: 'lead', byAuthor: {}, byIssue: {}, fallback: ['author', 'lead'] }, 'author'));
});

function fakePull(author, draft = false) {
  const pr = { user: { login: author }, state: 'open', draft, base: { ref: 'main' },
    assignees: [], requested_reviewers: [{ login: 'prettysorrow' }], requested_teams: [], body: 'Refs #3' };
  const calls = [];
  const github = {
    rest: {
      pulls: {
        get: async () => ({ data: pr }),
        removeRequestedReviewers: async ({ reviewers }) => {
          calls.push(['remove', reviewers]);
          pr.requested_reviewers = pr.requested_reviewers.filter(user => !reviewers.includes(user.login));
        },
        requestReviewers: async ({ reviewers }) => {
          calls.push(['request', reviewers]);
          pr.requested_reviewers.push(...reviewers.map(login => ({ login })));
        },
        listRequestedReviewers: async () => ({ data: { users: pr.requested_reviewers } }),
        listReviews: () => {},
      },
      issues: { addAssignees: async ({ assignees }) => { calls.push(['assign', assignees]); } },
    },
    paginate: async () => [],
  };
  return { pr, calls, args: { github, context: { repo: { owner: 'alken1t15', repo: 'simple-platformer' },
    payload: { pull_request: { number: 123 } } }, core: { info() {} } } };
}

test('Assignment replaces extra reviewer requests and includes Team Lead', async () => {
  const mock = fakePull('Nickovlev');
  await assign(mock.args);
  assert.deepEqual(mock.calls, [['assign', [policy.lead]], ['remove', ['prettysorrow']],
    ['request', ['VladimirKhmelev', policy.lead]]]);
});

test('Team Lead author is assignee and never a requested reviewer', async () => {
  const mock = fakePull(policy.lead);
  await assign(mock.args);
  assert.deepEqual(mock.pr.requested_reviewers.map(user => user.login), []);
  assert.deepEqual(mock.calls[0], ['assign', [policy.lead]]);
});

test('Draft waits for Ready for review and unaccepted reviewer access fails visibly', async () => {
  const draft = fakePull('Nickovlev', true);
  await assign(draft.args);
  assert.deepEqual(draft.calls, [['assign', [policy.lead]]]);
  const rejected = fakePull('Nickovlev');
  rejected.args.github.rest.pulls.requestReviewers = async () => { throw new Error('reviewer access pending'); };
  await assert.rejects(assign(rejected.args), /reviewer access pending/);
});
