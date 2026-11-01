# Cash, Payments and Register Sessions

Support register opening float, cashier assignment, cash in/out, paid-in/paid-out reason codes, cash drops, shift handover, blind close, counted cash, expected cash, variance, manager review and reconciliation.

Payment methods are configurable by legal entity and branch. Integrate external payment providers through adapters; never store raw card PAN or CVV. Store provider tokens/references only under PCI-aware controls and use hosted/terminal flows where possible.

Payment lifecycle: initiated → authorized → captured/settled or failed → refunded/chargeback/reconciled. Distinguish tender from settlement. Support split tender and partial refunds with references to original payment allocation.

Cash and payment events must reconcile to accounting control accounts. A closed shift is immutable except through approved adjustment/reopen workflow.
