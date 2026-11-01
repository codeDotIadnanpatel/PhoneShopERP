# Development Dependency Map

```mermaid
flowchart TD
 P00[00 Project preparation] --> P01[01 Platform foundation]
 P00 --> A[Accounting policy and event design]
 P01 --> P02[02 Products and inventory]
 P01 --> P05[05 Accounting foundation]
 P02 --> P03[03 Purchasing]
 P02 --> P04[04 POS and retail]
 P05 --> P03
 P05 --> P04
 P02 --> P06[06 Advanced operations]
 P04 --> P06
 P05 --> P06
 P02 --> P07[07 Multi-branch and mobile]
 P04 --> P07
 P05 --> P07
 P01 --> P08[08 Customer and management]
 P06 --> P08
 P07 --> P08
 P05 --> P09[09 Financial and global]
 P08 --> P09
 P06 --> P10[10 Production launch]
 P07 --> P10
 P08 --> P10
 P09 --> P10
 A --> P02
 A --> P03
 A --> P04
```

Accounting policy, event mapping and posting contracts begin during Phases 00/01. Phase 05 is not permission to defer financial integrity until later. Parallel work requires explicit module ownership, stable API/event contracts, schema ownership and integration tests.
