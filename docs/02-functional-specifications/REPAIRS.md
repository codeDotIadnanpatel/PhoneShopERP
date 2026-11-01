# Repair and Service Desk

## Ticket intake
Capture customer, device, IMEI/serial, reported fault, accessories received, condition, pre-existing damage, photos, consent, estimate and expected completion. Customer signs intake terms on desktop or mobile.

## Lifecycle
New Intake → Diagnosing → Awaiting Approval → Awaiting Parts → In Progress → Quality Check → Ready for Pickup → Completed / Cancelled / Unrepairable.

Tenants may configure statuses but cannot bypass mandatory controls such as customer approval where required.

## Work execution
Assign technician; record diagnosis, labour, time, parts and part serials; reserve and issue parts from stock; track supplier orders; record repair notes and before/after photos. Returned unused parts go back to inventory with traceable movement.

## Customer communication
Trigger SMS/email/WhatsApp through provider adapters at configurable milestones. Respect consent and communication preferences. Public tracking links must be unguessable, expiring/revocable and reveal only minimal ticket status.

## Closeout
Record final charges, payment, collection signature and repair warranty. Link replaced components and any device swap. Post labour revenue, parts cost, inventory issue and tax according to accounting rules.

## Sensitive credentials
Do not store device passcodes in ordinary ticket fields, event payloads, logs or notifications. Prefer customer unlock at intake. If temporary credential handling is unavoidable, require explicit consent, separate encrypted vault, narrow role access, access audit, short expiry and verified deletion.
