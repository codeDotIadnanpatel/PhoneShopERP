# Serialized Inventory and IMEI Lifecycle

## Identity
Track each device/serialized item as an individual asset. Capture IMEI1, IMEI2, serial number, manufacturer/model/variant, colour, storage, condition, source, ownership/consignment status and verification evidence. Normalize identifiers and prevent duplicate active asset identities within the applicable namespace. Support devices with multiple IMEIs and items with serial-only identity.

## Lifecycle
Expected → Received → Available → Reserved → In Transfer → In Repair / Quarantined → Sold → Returned / Warranty → Available, Refurbishment, Written Off or Disposed, as allowed by controlled transitions.

The lifecycle must distinguish physical location, ownership, commercial availability, reservation and condition. “Sold” is not a location. Consignment ownership is not the same as physical possession.

## Ledger
Every movement records asset ID, prior/new status, source/destination location, linked document, actor, timestamp, reason and evidence. Corrections use reversal/adjustment events; do not erase history.

## Operations
Scan on receiving, transfer dispatch/receipt, POS sale, return, repair intake, trade-in, stock count and warranty replacement. Support barcode, keyboard-wedge scanner and mobile camera input. Display full identifier only to authorized users; mask where appropriate.

## Controls
- Block selling an asset not available to the current branch/register.
- Warn clearly when asset belongs to another branch, is reserved, quarantined, sold or in repair.
- Require manager approval for high-risk status overrides.
- Record IMEI checks as evidence, not as an assertion that an external blacklist database was queried unless integration confirms it.
- Preserve original and replacement IMEI relationships for warranty swaps.

## Acceptance
A device detail view shows complete lifecycle and cost history. Duplicate IMEI intake is rejected or routed to a controlled exception process with evidence; it must not silently create a second device.
