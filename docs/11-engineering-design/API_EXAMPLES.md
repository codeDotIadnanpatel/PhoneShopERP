# Illustrative API Examples

**Important:** examples are conceptual JSON. They demonstrate required distinctions, not final field names, routes, enums or accounting behaviour. Align them with approved OpenAPI schemas.

## Create a product variant

```json
{
  "product": { "name": "Example smartphone", "brand": "Example", "category": "smartphone" },
  "variant": {
    "sku": "PHONE-256-BLK",
    "attributes": { "storage": "256GB", "colour": "Black", "region": "Global" },
    "barcode": "0000000000000",
    "currency": "SAR"
  }
}
```

## Receive a serialized device

```json
{
  "purchaseOrderId": "po_example",
  "receiptReference": "GRN-EXAMPLE",
  "device": {
    "variantId": "variant_example",
    "identifiers": [
      { "type": "IMEI1", "value": "000000000000000" },
      { "type": "IMEI2", "value": "000000000000001" }
    ],
    "condition": "new",
    "unitCost": { "amount": "2500.00", "currency": "SAR" }
  },
  "stockLocationId": "location_example",
  "idempotencyKey": "client-generated-unique-key"
}
```

## Complete a split-tender sale

```json
{
  "cartId": "cart_example",
  "expectedCartVersion": 7,
  "lines": [
    { "type": "serialized", "deviceUnitId": "device_example", "quantity": 1, "unitPrice": "2999.00" }
  ],
  "tenders": [
    { "method": "cash", "amount": "1000.00", "currency": "SAR" },
    { "method": "card_terminal", "amount": "1999.00", "currency": "SAR", "providerReference": "provider_ref" }
  ],
  "idempotencyKey": "sale-completion-unique-key"
}
```

The server must validate price, tax, device availability, cashier permissions, cash session and payment outcome. A pending terminal outcome must not be treated as a successful capture or retried as a new charge.

## Return a sold device

```json
{
  "originalSaleId": "sale_example",
  "lines": [
    {
      "originalSaleLineId": "line_example",
      "quantity": 1,
      "requestedDisposition": "inspection_required",
      "reasonCode": "customer_return"
    }
  ],
  "refund": {
    "allocations": [
      { "method": "original_tender", "amount": "2999.00", "currency": "SAR" }
    ]
  },
  "idempotencyKey": "return-unique-key"
}
```

Return authorization, refund policy and physical inspection are separate. Do not automatically put the device back into sellable stock.

## Submit an offline command batch

```json
{
  "deviceId": "registered-pos-device",
  "batchId": "batch-unique-id",
  "commands": [
    {
      "commandId": "command-unique-id",
      "schemaVersion": 1,
      "aggregateType": "Cart",
      "aggregateId": "cart-local-id",
      "expectedVersion": 3,
      "occurredAt": "2026-01-01T10:00:00Z",
      "commandType": "CartParked",
      "payload": { "cartLocalReference": "local-cart-123" }
    }
  ]
}
```

A local timestamp is diagnostic only. Server authorization, deduplication, conflict checks and authoritative timestamps still apply.
