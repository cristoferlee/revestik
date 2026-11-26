# Roadmap and delivery status

**Current baseline: 2026-11-26, `68638b8118c9583662a85599736031eea080b365`.**

The internal modular-monolith platform is operational. Public website features are deployed prelaunch; anonymous root redirects to `/es` (HTTP 200 observed), but commercial launch and indexing are not approved. Public Google login/registration remains disabled in Production. The legacy editor bridge and old administrative subdomain are retired.

SQL Standard S0 and the existing ACA capacity are retained. Persistent publication, additional CDN and ACA reduction are closed or cancelled as recorded in the [optimization resolution](REVESTIK-OPTIMIZATION-MASTER-PLAN.md).

| Active block | Scope |
| --- | --- |
| FINAL-02B | Documentation synchronization and review |
| FINAL-03A | Database integrity and public cart key |
| FINAL-03B | Public identity, security and abuse protection |
| FINAL-03C | Image optimization and confirmed orphan maintenance |
| FINAL-03D | Legal, SEO, errors and multilingual public acceptance |
| FINAL-04 | Commercial and functional verification |
| FINAL-05 | Evidence-driven refactor and duplicate removal |
| FINAL-06 | Release candidate, regression, security, recovery and release approval |

The [final closure plan](REVESTIK-FINAL-CLOSURE-PLAN.md) defines acceptance and evidence gaps. Historical checkpoints remain evidence, not independent active backlogs. No feature or infrastructure change is authorized by this roadmap alone. No dates are committed. Ordinary monitoring, backups and maintenance continue after closure.

Distinguish implemented, locally verified, deployed, reachable and formally launched. Follow INVESTIGATE → PLAN → APPROVE → IMPLEMENT → VERIFY → STOP.
