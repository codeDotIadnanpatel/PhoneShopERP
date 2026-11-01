# WPF Desktop POS Specification

## Shell and startup
Single-window shell with top header (tenant/shop, branch, register, user, sync status), persistent left navigation, central workspace and bottom status bar (shift, drawer, connectivity, last sync). Startup initializes local database and configuration, validates cached session, loads the user's default workspace and starts background sync without blocking the UI.

Do not promise sub-0.5-second startup until measured. Local login may be permitted offline only under device/session policy.

## Navigation
POS, Inventory, Purchasing, Customers, Trade-In, Repair Desk, Transfers, Cash/Shift, Reports, Settings. Visibility depends on permissions. Function-key shortcuts are configurable.

## POS layout
Persistent scan bar; cart grid; total/tax/discount; customer context; checkout actions; parked cart recall; clear blocked-IMEI warnings. Keep scan focus predictable after modals and actions.

## UI engineering
MVVM; async commands; cancellation; responsive UI thread; virtualization for large grids; keyboard navigation; accessible focus; reusable dialogs/drawers; localization and high-DPI support. No synchronous network or database operations on UI thread.

## Hardware abstraction
Scanner (keyboard wedge and supported SDKs), thermal printer, cash drawer, customer display and optional scale. Hardware failure must produce actionable diagnostics and safe fallback. Receipt template is configurable per legal entity/branch.
