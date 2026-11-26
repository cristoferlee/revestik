# Roadmap and delivery status

**Current baseline: 2026-11-26, `68638b8118c9583662a85599736031eea080b365`.**

The internal modular-monolith platform is operational. Public website features are deployed prelaunch; anonymous root redirects to `/es` (HTTP 200 observed), but commercial launch and indexing are not approved. Public Google login/registration remains disabled in Production. The legacy editor bridge and old administrative subdomain are retired.

SQL Standard S0 and the existing ACA capacity are retained. Persistent publication, additional CDN and ACA reduction are closed or cancelled as recorded in the [optimization resolution](REVESTIK-OPTIMIZATION-MASTER-PLAN.md).

| Active block | Scope |
| --- | --- |
| FINAL-02B | Documentation synchronization and review |
| FINAL-03A | Database integrity and public cart key |
| FINAL-03B | Implemented and locally verified. Real Google OAuth acceptance remains open. |
| FINAL-03C | Implemented and locally verified. Browser, container capacity and rollout acceptance remain open. |
| FINAL-03D | Legal, SEO, errors and multilingual public acceptance |
| FINAL-04 | Commercial and functional verification |
| FINAL-05 | Evidence-driven refactor and duplicate removal |
| FINAL-06 | Release candidate, regression, security, recovery and release approval |

The [final closure plan](REVESTIK-FINAL-CLOSURE-PLAN.md) defines acceptance and evidence gaps. Historical checkpoints remain evidence, not independent active backlogs. No feature or infrastructure change is authorized by this roadmap alone. No dates are committed. Ordinary monitoring, backups and maintenance continue after closure.

The combined pre-commit candidate is based on `88c86b9532c24c915857063ef14baf266d20bbfd`. The integrated local Release run passed 1,492 tests. Follow-up focused runs covered the tests added after that run began. The production baseline above remains the last recorded deployment evidence.

Distinguish implemented, locally verified, deployed, reachable and formally launched. Follow INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.
