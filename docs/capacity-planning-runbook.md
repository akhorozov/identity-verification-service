# CosmosDB Capacity Planning Runbook

**Service**: Address Validation API  
**Last updated**: <!-- auto-updated by CI -->  
**Owner**: Platform Engineering  
**SRS Ref**: Section 7.4, T15 #140

---

## 1. Overview

The Address Validation API uses two Azure CosmosDB containers provisioned with
**autoscale throughput** (400–4 000 RU/s per container). This runbook describes:

- How RUs are consumed by each workload
- How to monitor and react to throttling
- How to scale up/down safely
- TTL, partition key, and change feed considerations

---

## 2. Container Summary

| Container | Partition Key | Default TTL | Max RU/s (autoscale) | Purpose |
|-----------|--------------|-------------|----------------------|---------|
| `cache` | `/stateAbbreviation` | 90 days | 4 000 | L2 validated-address cache |
| `audit-events` | `/correlationId` | 365 days | 4 000 | Append-only audit trail |
| `audit-leases` | `/id` | none | 1 000 | Change Feed processor leases |

---

## 3. RU Cost Reference

| Operation | Approx. RU |
|-----------|-----------|
| Point read (1 KB document) | 1 RU |
| Point write / upsert (1 KB) | 5–7 RU |
| Cross-partition query (no index hit) | 10–50 RU |
| In-partition query (index hit) | 2–5 RU |
| Change Feed read (batch 100 items) | 1 RU |

**Cache container** (hot path):
- Cache hit: 1 RU (point read by `id` + partition key)
- Cache write: ~6 RU (upsert on validation)
- At 500 RPS with 30 % cache-miss rate: ≈ 350 + 1 050 = **1 400 RU/s peak**

**Audit container** (write-heavy, read-rare):
- Write per request: ~6 RU
- At 500 RPS: **3 000 RU/s sustained** — within 4 000 RU/s ceiling

> **Rule of thumb**: total 4 400 RU/s peak across both containers fits within provisioned
> 8 000 RU/s with ≥ 45 % headroom.

---

## 4. Partition Key Design

### `cache` container — `/stateAbbreviation`

- 50 distinct values (US states + DC + territories)
- Each partition holds ~2 % of total data
- Avoids hot partitions for popular states because autoscale distributes RUs
- **Limitation**: Arizona (AZ) and California (CA) addresses are high volume; monitor
  individual partition RU consumption in Azure Monitor

### `audit-events` container — `/correlationId`

- High cardinality (UUID per request) → near-perfect distribution
- Point reads are cheap; cross-partition queries are rare (audit reports only)
- No hot-partition risk

---

## 5. TTL Management

| Container | TTL | Rationale |
|-----------|-----|-----------|
| `cache` | 90 days (7 776 000 s) | Balances freshness with USPS address change frequency |
| `audit-events` | 365 days (31 536 000 s) | Regulatory retention minimum (SRS Section 7.1) |

### Changing TTL

1. Update `infra/bicep/modules/cosmos-autoscale.bicep` → `defaultTtl` property.
2. Deploy via `az deployment group create` or merge to `main` to trigger CI/CD.
3. TTL changes apply only to **new** documents; existing documents retain the TTL
   value embedded at write time.

To reset TTL on existing cache items, flush the cache:

```http
POST /cache/flush
Api-Version: 1.0
X-Api-Key: <admin-key>
```

---

## 6. Autoscale Throughput

### Current settings (both containers)

```bicep
autoscaleSettings: { maxThroughput: 4000 }   // scales 400–4 000 RU/s
```

### Scale-up procedure

1. Identify throttling via **Azure Monitor** → CosmosDB → `429 TooManyRequests` metric.
2. Calculate required peak RU/s from the formula in Section 3.
3. Update `maxThroughput` in `cosmos-autoscale.bicep`.
4. Submit PR → merge → CI/CD deploys the change online (zero downtime).

### Scale-down procedure

Autoscale automatically scales down to the minimum (10 % of max) during idle periods.
Manual scale-down of `maxThroughput` is safe at any time; it takes effect within minutes.

### Cost estimate

| Max RU/s | Monthly cost (East US) |
|----------|------------------------|
| 4 000 | ~$29 USD |
| 10 000 | ~$73 USD |
| 40 000 | ~$292 USD |

*Prices are approximate. Verify at [Azure Pricing Calculator](https://azure.microsoft.com/en-us/pricing/calculator/).*

---

## 7. Change Feed Processor

The `ChangeFeedProcessorService` monitors `audit-events` for new documents and emits
structured log entries to Application Insights.

| Setting | Value | Config Key |
|---------|-------|-----------|
| Poll interval | 5 seconds | hardcoded |
| Lease container | `audit-leases` | `CosmosDb:LeaseContainerName` |
| Processor name | `audit-change-feed` | `CosmosDb:ChangeFeedProcessorName` |
| Instance name | `$HOSTNAME` | `Environment.MachineName` |

### Monitoring

Check logs for `ChangeFeed |` prefix entries in Application Insights:

```kusto
traces
| where message startswith "ChangeFeed |"
| summarize count() by bin(timestamp, 5m), tostring(customDimensions.EventType)
| render timechart
```

### Reprocessing (lag recovery)

The processor automatically resumes from the last committed lease position after a
restart. To reprocess from the beginning:

1. Delete all documents in `audit-leases` container.
2. Restart the container app — the processor will rebuild leases from the start.

> ⚠️ Full reprocessing at high volume generates significant RU consumption on both
> the monitored container and lease container. Schedule during off-peak hours.

---

## 8. Monitoring and Alerting

### Key metrics (Azure Monitor)

| Metric | Warning threshold | Critical threshold |
|--------|------------------|--------------------|
| `NormalizedRUConsumption` | > 70 % | > 90 % |
| `ServerSideLatency` (P99) | > 50 ms | > 200 ms |
| `TotalRequestUnits` (429) | > 0 | > 100 / min |
| `DocumentCount` growth / day | — | > 10 × baseline |

### Recommended alert rules (Bicep / ARM)

```bicep
resource highRUAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: 'cosmos-high-ru-consumption'
  // ...criteria: NormalizedRUConsumption > 80 for 5 minutes
}
```

---

## 9. Operational Checklist

- [ ] Verify autoscale `maxThroughput` is ≥ calculated peak RU/s (Section 3)
- [ ] Confirm TTL is set correctly on both containers (`az cosmosdb sql container show`)
- [ ] Check Change Feed processor logs are appearing in Application Insights
- [ ] Confirm `audit-leases` container exists and has documents after first deploy
- [ ] Set up Azure Monitor alert for `NormalizedRUConsumption > 80 %`
- [ ] Review partition key distribution monthly for `cache` container
- [ ] Run `az cosmosdb sql container show` after each infra deploy to verify settings

---

## 10. References

- [Azure CosmosDB autoscale docs](https://learn.microsoft.com/en-us/azure/cosmos-db/provision-throughput-autoscale)
- [Change Feed processor guide](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/change-feed-processor)
- [Request unit guide](https://learn.microsoft.com/en-us/azure/cosmos-db/request-units)
- `infra/bicep/modules/cosmos-autoscale.bicep` — provisioning source of truth
- `src/AddressValidation.Api/Infrastructure/Services/ChangeFeed/` — processor implementation
