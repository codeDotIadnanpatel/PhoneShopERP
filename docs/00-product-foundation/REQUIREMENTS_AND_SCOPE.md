# Product Requirements and Scope

## Must-have domain capabilities
Tenant and branch setup; catalogue; serialized and bulk inventory; suppliers and purchasing; POS; payment allocation; cash sessions; basic customer records; returns; full accounting foundations; audit trail; role-based permissions; offline local persistence and sync; imports/exports; reporting.

## Complete product capabilities
Add trade-in/buyback, repairs, warranties, transfers, cycle counts, consignment, advanced price/margin rules, loyalty, staff commissions, customer portal, mobile app, notifications, integrations, SaaS subscriptions, advanced accounting, tax/localization packs, analytics and enterprise administration.

## Cross-cutting requirements
- Every write has tenant context and authorization.
- Every stock movement has a source, destination where relevant, quantity/identity, actor, timestamp and reason.
- Every financial posting is balanced and traceable to its source document.
- Every business document has a lifecycle and permitted state transitions.
- Critical actions require idempotency and duplicate-submission protection.
- Reversals create compensating records; posted history is not silently edited.
- All user-facing dates, numbers, money and documents respect legal-entity locale settings.
- Integrations fail independently and retry safely.

## Deferred or integration-dependent
External market prices, carrier activations, government/police reporting, ID verification, payment processing, messaging channels and e-commerce marketplaces are adapters. Core workflows must remain useful when an adapter is unavailable.
