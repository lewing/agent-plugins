# Generating Recommendations

After the script outputs the `[CI_ANALYSIS_SUMMARY]` JSON block, **you** synthesize recommendations. Do not parrot the JSON — reason over it.

## Decision Logic

Read `recommendationHint` as a starting point, then layer in context:

| Hint | Action |
|------|--------|
| `BUILD_SUCCESSFUL` | No failures. Confirm CI is green. |
| `KNOWN_ISSUES_DETECTED` | Known tracked issues found — but this does NOT mean all failures are covered. Check the Build Analysis check status: if it's red, some failures are unmatched. Only recommend retry for failures that specifically match a known issue; investigate the rest. |
| `LIKELY_PR_RELATED` | Failures correlate with PR changes. Lead with "fix these before retrying" and list `correlatedFiles`. |
| `POSSIBLY_TRANSIENT` | Failures could not be automatically classified — does NOT mean they are transient. Use `failedJobDetails` to investigate each failure individually. |
| `REVIEW_REQUIRED` | Could not auto-determine cause. Review failures manually. |
| `MERGE_CONFLICTS` | PR has merge conflicts — CI won't run. Tell the user to resolve conflicts. Offer to analyze a previous build by ID. |
| `NO_BUILDS` | No AzDO builds found (CI not triggered). Offer to check if CI needs to be triggered or analyze a previous build. |

## Layering Nuance

Then layer in nuance the heuristic can't capture:

- **Mixed signals**: Some failures match known issues AND some correlate with PR changes → separate them. Known issues = safe to retry; correlated = fix first.
- **Canceled jobs with recoverable results**: If `canceledJobNames` is non-empty, mention that canceled jobs may have passing Helix results (see [failure-interpretation.md](failure-interpretation.md) — Recovering Results).
- **Build still in progress**: If `lastBuildJobSummary.pending > 0`, note that more failures may appear.
- **Multiple builds**: If `builds` has >1 entry, `lastBuildJobSummary` reflects only the last build — use `totalFailedJobs` for the aggregate count.
- **BuildId mode**: `knownIssues` and `prCorrelation` won't be populated. Say "Build Analysis and PR correlation not available in BuildId mode."

## How to Retry

- **AzDO builds**: Comment `/azp run {pipeline-name}` on the PR (e.g., `/azp run dotnet-sdk-public`)
- **All pipelines**: Comment `/azp run` to retry all failing pipelines
- **Helix work items**: Cannot be individually retried — must re-run the entire AzDO build

## Tone and Output Format

Be direct. Lead with the most important finding. Structure your response as:
1. **Summary verdict** (1-2 sentences) — Is CI green? Failures PR-related? Known issues?
2. **Failure details** (2-4 bullets) — what failed, why, evidence
3. **Recommended actions** (numbered) — retry, fix, investigate. Include `/azp run` commands.

Synthesize from: JSON summary (structured facts) + human-readable output (details/logs) + Step 0 context (PR type, author intent).
