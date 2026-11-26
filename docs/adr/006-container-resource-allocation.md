# ADR-006 — Retain current Container Apps allocation

**Status:** Accepted, 2026-11-26 (FINAL-02A).

**Decision:** Retain 0.5 vCPU, 1 GiB RAM and min=max=1 replica. Close ACA-01 without change and cancel remaining benchmark/reduction work. Temporary local SQL container and internal network were removed.

**Rationale:** Seven-day working-set average 335 MiB, P95 387 MiB, peak 422 MiB; earlier observed peak 467 MiB. A 512 MiB limit leaves limited headroom for PDF, uploads and concurrent work. Estimated savings of $8.35 per 30 days do not justify the current risk/effort. Multiple replicas also require background-job coordination.

**Limitations:** No completed comparative benchmark; 512 MiB is not certified or proven infeasible. Observe real workload as routine operations; any capacity change requires new evidence and approval.
