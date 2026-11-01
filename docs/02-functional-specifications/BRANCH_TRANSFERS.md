# Branch Transfers and Stock Counts

## Transfer workflow
Draft → Approved → Dispatched → In Transit → Partially Received / Received → Closed. A transfer has source/destination legal entity and branch, item list, serials/quantities, shipper, dispatch evidence, receipt evidence and variance handling.

For serialized devices, sender scans exact IMEIs; receiver independently scans receipt. Mismatches, missing units and damage are exceptions, not silently accepted. For bulk goods, support partial quantity receipt and variance.

## Stock counts
Support full and cycle counts, blind counts, mobile scanning, freeze/snapshot policy, recount, variance approval and posted adjustment. Count results must not rewrite historical movement records.

## Consignment
Track legal owner separately from physical location and sale availability. Define supplier settlement trigger, commission/fee and return rules per agreement.
