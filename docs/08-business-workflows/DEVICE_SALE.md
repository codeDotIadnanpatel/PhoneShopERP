# Device Sale Workflow

1. Cashier scans device IMEI/barcode.
2. System verifies branch, asset status, reservation, condition and sellable status.
3. Add accessories/services; bundle rules and price guardrails apply.
4. Attach customer where required.
5. Apply discounts within permission/approval limits.
6. Checkout with one or multiple tenders.
7. Commit sale, inventory issue, tax, payment allocation and accounting event idempotently.
8. Print/send receipt with device identity and warranty terms.
9. Sync cloud acknowledgement; display pending/conflict state accurately if offline.
