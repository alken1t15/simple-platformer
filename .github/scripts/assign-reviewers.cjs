const fs = require('node:fs');

function chooseReviewers(policy, author, body = '') {
  const same = (a, b) => a.toLowerCase() === b.toLowerCase();
  const issue = body.match(/\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?)\s+#(\d+)\b/i)?.[1];
  const authorDefault = Object.entries(policy.byAuthor).find(([login]) => same(login, author))?.[1];
  const candidates = [policy.byIssue[issue], authorDefault, ...policy.fallback].filter(Boolean);
  const reviewer = candidates.find(login => !same(login, author) && !same(login, policy.lead));
  if (!reviewer) throw new Error('No reviewer distinct from author and Team Lead');
  return same(author, policy.lead) ? [reviewer] : [reviewer, policy.lead];
}

async function assign({ github, context, core }) {
  const policy = JSON.parse(fs.readFileSync('.github/review-policy.json', 'utf8'));
  const { owner, repo } = context.repo;
  const pull_number = context.payload.pull_request.number;
  const { data: pr } = await github.rest.pulls.get({ owner, repo, pull_number });
  if (pr.state !== 'open' || pr.base.ref !== 'main') return;
  // Team Lead is involved in every PR, including their own, through the Assignee field.
  if (!pr.assignees.some(user => user.login.toLowerCase() === policy.lead.toLowerCase())) {
    await github.rest.issues.addAssignees({ owner, repo, issue_number: pull_number, assignees: [policy.lead] });
  }
  if (pr.draft) {
    core.info('Draft: reviewer requests will be sent when ready for review');
    return;
  }
  const wanted = chooseReviewers(policy, pr.user.login, pr.body || '');
  const current = pr.requested_reviewers.map(user => user.login);
  const teams = pr.requested_teams.map(team => team.slug);
  const remove = current.filter(login => !wanted.some(target => target.toLowerCase() === login.toLowerCase()));
  if (remove.length || teams.length) {
    await github.rest.pulls.removeRequestedReviewers({ owner, repo, pull_number, reviewers: remove, team_reviewers: teams });
  }
  const missing = wanted.filter(login => !current.some(existing => existing.toLowerCase() === login.toLowerCase()));
  if (missing.length) {
    // Fail visibly if a selected reviewer has not accepted access; never replace them silently.
    await github.rest.pulls.requestReviewers({ owner, repo, pull_number, reviewers: missing });
  }
  const { data: actual } = await github.rest.pulls.listRequestedReviewers({ owner, repo, pull_number });
  const reviews = await github.paginate(github.rest.pulls.listReviews, { owner, repo, pull_number, per_page: 100 });
  for (const login of wanted) {
    const requested = actual.users.some(user => user.login.toLowerCase() === login.toLowerCase());
    const reviewed = reviews.some(review => review.user?.login.toLowerCase() === login.toLowerCase());
    if (!requested && !reviewed) throw new Error(`Reviewer not assigned: ${login}`);
  }
  core.info(`Review policy: ${wanted.join(', ')}; author excluded`);
}

module.exports = { chooseReviewers, assign };
