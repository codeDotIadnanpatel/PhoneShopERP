# Hardware Integration Strategy

**Status:** hardware-neutral abstraction plan. No specific printer, scanner, drawer or terminal model is certified by this document.

## Device classes

| Device class | Typical connection | Integration approach | Important behaviour |
|---|---|---|---|
| Barcode scanner | USB HID, Bluetooth HID, serial | Prefer keyboard-wedge compatibility; optional native adapter | Prefix/suffix, focus handling, scan debounce, barcode validation |
| Receipt printer | USB, Ethernet, Bluetooth, OS driver | Printer abstraction with capability profile; ESC/POS only where validated | Paper width, cutter, logo, Arabic/Unicode support, drawer kick |
| Cash drawer | RJ11/RJ12 via printer, USB controller | Trigger through certified printer/controller abstraction | Open only after authorized cash workflow; log trigger outcome where available |
| Payment terminal | Vendor SDK/API, USB, network | Provider adapter; terminal owns card entry | Never collect/store PAN or CVV in ERP |
| Label printer | USB, Ethernet, Bluetooth | Template/rendering abstraction | Label dimensions, barcode quality, localization |
| Customer display | USB/serial/network | Optional display adapter | Do not expose customer personal data |
| Scale (if later supported) | Serial/USB/network | Explicit measurement adapter | Unit, calibration and legal metrology review |

## Architecture guidance

- Define interfaces such as `IBarcodeInput`, `IReceiptPrinter`, `ICashDrawer`, `IPaymentTerminal`, `ILabelPrinter`; keep vendor SDKs in adapters.
- Device discovery and capability reporting must not block checkout indefinitely.
- A missing printer must not corrupt or duplicate a completed sale; receipt reprint is a separate audited action.
- Payment terminal outcome must be reconciled by transaction/reference; timeout is an unknown state, not an automatic decline or approval.
- Provide a manual fallback path where operationally safe and legally permitted.
- Keep device-specific configuration per POS station, not global tenant settings.

## Compatibility matrix (to be maintained)

For every supported model record vendor/model, firmware, OS version, connection type, driver/SDK version, tested features, known limitations, certification date and test owner. Mark devices as `tested`, `limited`, `unsupported` or `not yet tested`.

## Acceptance tests

Scanner input in all relevant fields; duplicate scan prevention; printer offline during sale; reprint authorization; paper-out and cutter failures; drawer open authorization; terminal timeout/reconciliation; terminal cancellation; disconnect/reconnect; Arabic/Latin receipt rendering; multi-station configuration.

## Open decisions

- Minimum supported Windows versions and driver deployment strategy.
- ESC/POS support scope and printer certification list.
- Payment terminal providers and supported countries.
- Whether Flutter staff app supports Bluetooth peripherals directly.
- Whether customer-facing displays and scales are in initial scope.
