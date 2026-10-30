#Requires -Version 7.4
param([switch]$Apply)
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$repository = 'alken1t15/simple-platformer'

function Invoke-GitHubJson([string[]]$Arguments) {
    $output = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub request failed: $($Arguments -join ' ')" }
    return ($output -join "`n") | ConvertFrom-Json -Depth 30
}

$owner = Invoke-GitHubJson @('api', 'users/alken1t15')
$conditions = @{ ref_name = @{ include = @('refs/heads/main'); exclude = @() } }
$pullRequest = @{
    type = 'pull_request'
    parameters = @{
        required_approving_review_count = 0
        dismiss_stale_reviews_on_push = $false
        require_code_owner_review = $false
        require_last_push_approval = $false
        required_review_thread_resolution = $false
        allowed_merge_methods = @('merge', 'squash', 'rebase')
    }
}
$checks = @('build', 'documents', 'graphics-smoke') | ForEach-Object {
    @{ context = $_; integration_id = 15368 }
}
$base = @{
    name = 'main: PR and required CI'
    target = 'branch'
    enforcement = 'active'
    conditions = $conditions
    bypass_actors = @()
    rules = @(
        @{ type = 'deletion' }
        @{ type = 'non_fast_forward' }
        $pullRequest
        @{
            type = 'required_status_checks'
            parameters = @{
                strict_required_status_checks_policy = $false
                do_not_enforce_on_create = $false
                required_status_checks = @($checks)
            }
        }
    )
}
$approvals = @{
    name = 'main: reviewer and Team Lead'
    target = 'branch'
    enforcement = 'active'
    conditions = $conditions
    bypass_actors = @(@{ actor_id = $owner.id; actor_type = 'User'; bypass_mode = 'pull_request' })
    rules = @(@{
        type = 'pull_request'
        parameters = @{
            required_approving_review_count = 2
            dismiss_stale_reviews_on_push = $false
            require_code_owner_review = $true
            require_last_push_approval = $false
            required_review_thread_resolution = $false
            allowed_merge_methods = @('merge', 'squash', 'rebase')
        }
    })
}

if (-not $Apply) {
    @($base, $approvals) | ConvertTo-Json -Depth 20
    Write-Host 'Dry run. Use -Apply to configure only alken1t15/simple-platformer.'
    return
}

$null = Invoke-GitHubJson @('api', "repos/$repository/branches/main")
$existing = @(Invoke-GitHubJson @('api', "repos/$repository/rulesets", '--paginate'))
foreach ($payload in @($base, $approvals)) {
    $matches = @($existing | Where-Object name -eq $payload.name)
    if ($payload.name -eq $approvals.name -and $matches.Count -eq 0) {
        $matches = @($existing | Where-Object name -eq 'main: two human approvals')
    }
    if ($matches.Count -gt 1) { throw "Duplicate ruleset name: $($payload.name)" }
    $endpoint = "repos/$repository/rulesets"
    $method = 'POST'
    if ($matches.Count -eq 1) {
        $endpoint += "/$($matches[0].id)"
        $method = 'PUT'
    }
    $response = ($payload | ConvertTo-Json -Depth 20) | & gh api --method $method $endpoint --input -
    if ($LASTEXITCODE -ne 0) { throw "Cannot apply ruleset: $($payload.name)" }
    $saved = ($response -join "`n") | ConvertFrom-Json -Depth 30
    $actual = Invoke-GitHubJson @('api', "repos/$repository/rulesets/$($saved.id)")
    if ($actual.enforcement -ne 'active' -or
        $actual.conditions.ref_name.include -cnotcontains 'refs/heads/main' -or
        @($actual.conditions.ref_name.exclude).Count -ne 0) {
        throw "Ruleset is not active on main: $($payload.name)"
    }
    if ($payload.name -eq $base.name) {
        $types = @($actual.rules.type)
        foreach ($type in 'deletion', 'non_fast_forward', 'pull_request', 'required_status_checks') {
            if ($types -notcontains $type) { throw "Missing base rule: $type" }
        }
        if (@($actual.bypass_actors).Count -ne 0) { throw 'Base protection must have no bypass' }
        $basePr = @($actual.rules | Where-Object type -eq 'pull_request')
        if ($basePr.Count -ne 1 -or $basePr[0].parameters.required_approving_review_count -ne 0) {
            throw 'Base rule must preserve the own Team Lead PR exception; CI has no bypass'
        }
        $actualChecks = @(($actual.rules | Where-Object type -eq 'required_status_checks').parameters.required_status_checks)
        foreach ($check in $checks) {
            if (@($actualChecks | Where-Object { $_.context -eq $check.context -and $_.integration_id -eq 15368 }).Count -ne 1) {
                throw "Missing bound GitHub Actions check: $($check.context)"
            }
        }
    } else {
        $pr = @($actual.rules | Where-Object type -eq 'pull_request')
        $bypass = @($actual.bypass_actors)
        if ($pr.Count -ne 1 -or $pr[0].parameters.required_approving_review_count -ne 2 -or
            -not $pr[0].parameters.require_code_owner_review -or
            $bypass.Count -ne 1 -or $bypass[0].actor_id -ne $owner.id -or
            $bypass[0].actor_type -ne 'User' -or $bypass[0].bypass_mode -ne 'pull_request') {
            throw 'Approval rule differs from the intended Team Lead exception'
        }
    }
    Write-Host "Verified: $($actual.name) (id=$($actual.id))"
}
