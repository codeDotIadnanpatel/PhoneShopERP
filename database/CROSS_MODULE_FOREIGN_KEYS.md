# Cross-module foreign keys (review register)

ADR-0014 permits cross-module foreign keys where they protect essential referential
integrity inside the same PostgreSQL database. Each key is recorded here with its
owning module, dependent module and extraction implication. Unnecessary coupling is
not permitted, and a cross-module *read* still goes through a module contract.

| Key | Referencing table (dependent module) | Referenced table (owning module) | Why the database must enforce it | Extraction implication |
|---|---|---|---|---|
| `fk_membership_tenant` | `identity.membership` (Identity & Access) | `tenancy.tenant` (Tenancy) | A membership without a tenant is meaningless and would break every tenant grant. | Extracting Identity requires a tenant-existence check at the boundary or keeping the tenant table shared. |
| `fk_session_tenant` | `identity.session` (Identity & Access) | `tenancy.tenant` (Tenancy) | A session must never name a tenant that does not exist. | Same as above; session creation already runs inside tenant context. |
| `fk_device_tenant` | `identity.device` (Identity & Access) | `tenancy.tenant` (Tenancy) | A device registration is tenant-owned. | Same as above. |
| `fk_role_tenant` | `identity.role` (Identity & Access) | `tenancy.tenant` (Tenancy) | Roles are tenant-owned and cannot be shared across tenants. | Same as above. |
| `fk_subscription_tenant` | `saas.subscription` (SaaS Platform) | `tenancy.tenant` (Tenancy) | A subscription exists only for a real tenant. | Extracting billing requires a tenant directory contract. |
| `fk_entitlement_tenant` | `saas.entitlement` (SaaS Platform) | `tenancy.tenant` (Tenancy) | An entitlement without a tenant is unusable. | Same as above. |
| `fk_usage_record_tenant` | `saas.usage_record` (SaaS Platform) | `tenancy.tenant` (Tenancy) | Usage must be attributable. | Same as above. |
| `fk_billing_attempt_tenant` | `saas.billing_attempt` (SaaS Platform) | `tenancy.tenant` (Tenancy) | Billing attempts must be attributable. | Same as above. |

## Rules for adding a new entry

1. The key must protect integrity that cannot be repaired later without data loss.
2. The PR/ADR must record both modules, the extraction implication and the
   alternative considered (application validation, event, soft reference).
3. The key must be composite and tenant-leading (`tenant_id, id`) whenever the
   referenced row is tenant-owned.
4. No cross-module key may reference a mutable business fact that the owning module
   intends to relocate.

## Not permitted

- A foreign key added merely to simplify a join.
- A key that would make an otherwise extractable module unable to leave the
  database without a data migration.
- Cross-schema writes performed by a module that does not own the table.
