# Limits and next step

## Limitations

- **Value check, not meaning check** (see below). The fact checker confirms that a stated date exists in the
  record, not that it is attached to the right field ("shipped on" vs "arrives on").
- **Conservative extraction.** Negation is recognized only right before the status phrase
  ("has not been shipped", "aún no ha sido enviado"); a negation further away is not, and the
  draft escalates. That is a false positive by design.
- **Two languages only** (English, Spanish), chosen per ticket. In a Spanish ticket `$1,500` reads as
  1.5 and escalates; mixed formats are treated conservatively.
- **An edit skips the judge.** The reviewer's own version passes the deterministic rules again but
  no model reads it: the reviewer answers for it.
- **The judge can be wrong both ways.** A false negative only ships if every verifiable check also
  passed.
- In-memory stores and outbox; runs live in the API's memory and are lost on restart (the inbox
  in Postgres survives and marks those rows). No auth
  (see decision 11). Not production code.

## Next step: reading the draft

**What the rules do today.** Before a draft can go out on its own, the policy extracts every
factual claim from the text and compares it with the ticket:

| Claim | English | Spanish |
|---|---|---|
| Order id | `#A-1042` | `#A-1042` |
| Dates | `November 12`, `Nov 12th, 2026`, `2026-11-12` | `12 de noviembre`, `12/11`, `2026-11-12` |
| Amounts | `$129.90`, `1,500 USD` | `$1.500`, `$89,90`, `89,90 USD` |
| Status | shipped, delivered, being processed, cancelled | se despachó, fue entregado, en preparación, cancelado |
| Time promises | today, tomorrow, within…, in two days, next week | hoy, mañana, dentro de…, en dos días, la próxima semana |

Anything else that looks like a claim (a stray number, a format it does not know) is not guessed:
**the draft goes to a human.** That keeps auto-send safe, at the cost of extra reviews.

**Where rules stop.** Rules check *values*, not *meaning*. "Shipped on November 21" passes if
November 21 is any date on the order, even though it is the delivery date. Negation only works
when it sits right before the status ("has not been shipped"), not further away. Covering every
phrasing with rules is a losing game.

**Next step.** Let the LLM *read* and the code *decide*: an extractor model returns the claims it
finds as typed JSON, and the code compares each field with the ticket.

```json
{ "orderId": "A-1042",
  "dates":   [{ "field": "shippedOn", "value": "2026-11-21" }],
  "amounts": [], "status": "Shipped", "timePromises": [] }
```

`shippedOn = 2026-11-21` vs ticket `2026-11-12` → mismatch → human. The model never approves; it
only turns free text into data the code can check. Because the extractor can miss things too,
the rules stay as a second reader: if either finds an unverifiable claim, or they disagree, the
draft goes to a human.
