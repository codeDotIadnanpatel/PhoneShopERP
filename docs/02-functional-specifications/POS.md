# Point of Sale

## Design
The WPF POS is a fixed-frame, single-window workspace optimized for high-pressure retail. Provide a persistent scan/search input, fast cart grid, clear totals and an action area. Support keyboard-first operation and touch-friendly controls.

Suggested shortcuts: F1 POS/home, F2 checkout, F3 trade-in, F4 repair desk, F5 park cart, F6 reports/shift, F7 settings. Shortcuts must be configurable and displayed in the UI; avoid collisions with OS or assistive technology.

## Cart
Scan serialized IMEI/barcode and bulk product barcodes. Show item, variant, quantity, serial/IMEI, price, discount, tax and margin where permitted. Support bundles, price overrides, customer attachment, notes, quote conversion and multiple parked carts.

## Parked carts
Park action snapshots cart lines, price/discount context, customer reference, selected IMEIs, creator/register, timestamp and expiry policy. If stock is reserved, create a scoped lease/reservation; display whether reservation is confirmed locally or centrally. Recall lists parked carts with owner, age, items and total. Revalidate prices, permissions, stock and reservation before checkout. Expired or conflicting carts require clear resolution; do not silently sell stale reserved stock.

## Checkout
Support split tenders across cash, card, bank transfer, wallet, gift/store credit, trade-in credit and configured payment methods. Each tender has amount, currency, provider/reference and settlement status. Handle partial payment, deposits and approved account credit where enabled.

## Completion
Commit sale, inventory issue, payment allocation, tax calculation, invoice/receipt and accounting event idempotently. Print or deliver digital receipt. Include serial/IMEI and applicable warranty terms. If offline, clearly mark transaction pending cloud synchronization.

## Safety
Void/refund/discount limits; manager approval; duplicate scan prevention; clear blocked-item messages; undo only through audited compensating actions. No silent deletion of completed sales.
