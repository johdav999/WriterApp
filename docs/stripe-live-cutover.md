# Stripe Live Billing Cutover

This runbook is for moving Prosa from Stripe test mode to live billing using the current repo structure.

It is based on the current implementation in:
- `StripeOptions` under the `Stripe` config section
- live/test separation safeguards on persisted Stripe linkage
- admin diagnostics:
  - `GET /api/admin/stripe/readiness`
  - `GET /api/admin/stripe/price-mapping-health`
  - `POST /api/admin/stripe/resync`

## 1. Preconditions

Do not start live cutover until all of these are true:
- production deploy is running the current billing code
- startup no longer relies on legacy `Stripe:Billing` fallback
- `GET /api/admin/stripe/readiness` is available to admins
- `GET /api/admin/stripe/price-mapping-health` is available to admins
- production uses HTTPS on the real customer domain

## 2. Stripe Dashboard Setup

In Stripe live mode:

1. Create or verify products and prices for:
   - Standard
   - Professional
2. Copy the live price IDs.
3. Enable the Stripe billing portal.
4. Register the live webhook endpoint:
   - `POST https://<your-app-domain>/api/stripe/webhook`
5. Subscribe the webhook to these events:
   - `checkout.session.completed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `invoice.paid`
   - `invoice.payment_failed`
6. Copy the live webhook signing secret.

## 3. Production Environment Variables

Set these in production App Service settings:

- `Stripe__Enabled=true`
- `Stripe__WebhookHandlingEnabled=true`
- `Stripe__Mode=live`
- `Stripe__SecretKey=sk_live_...`
- `Stripe__WebhookSecret=whsec_...`
- `Stripe__Prices__Standard__LivePriceId=price_...`
- `Stripe__Prices__Pro__LivePriceId=price_...`
- `AppUrls__PublicBaseUrl=https://app.prosa-app.com`

Recommended additional settings:

- `Stripe__BillingPortalReturnUrl=/app/account`
- `Stripe__Checkout__SuccessPath=/app/account/billing?success=1&session_id={CHECKOUT_SESSION_ID}`
- `Stripe__Checkout__CancelPath=/app/account/billing?canceled=1`
- `Stripe__Checkout__BaseUrl=https://app.prosa-app.com`

Notes:
- Live mode only requires live price IDs for runtime operation, but keep test price IDs configured in non-production environments.
- Never put Stripe secrets in `appsettings.Production.json`.

## 4. Database Handling

Recommended:
- use a production database that does not contain test Stripe linkage for real users

Current repo safety behavior:
- persisted billing rows store Stripe mode
- opposite-mode linkage is detected and ignored
- webhook logs also store Stripe mode

Still recommended:
- do not use the same database for ongoing test and live customer billing
- do not rely on mixed-mode linkage in one shared environment as a normal operating model

If production currently contains test Stripe linkage:
1. inspect affected rows before go-live
2. clear or reset stale Stripe linkage for those users before enabling live traffic
3. verify `GET /api/admin/stripe/readiness` no longer reports mixed-mode billing records

## 5. Pre-Go-Live Checks

Before enabling customer traffic:

1. Deploy the production build.
2. Restart the app after all Stripe settings are in place.
3. Check startup logs:
   - Stripe enabled/disabled
   - resolved mode
   - webhook secret present
   - active-mode prices configured
4. Call:
   - `GET /api/admin/stripe/readiness`
   - expected result: `readiness = ready`
5. Call:
   - `GET /api/admin/stripe/price-mapping-health`
   - expected result:
     - no unknown live price mappings
     - live Standard and Professional prices present
6. Confirm webhook endpoint registration in Stripe live dashboard.

## 6. End-To-End Validation

Run these validations in live mode with controlled internal accounts before broad release.

### Live Standard checkout
1. Sign in as a production test user.
2. Start Standard checkout from the billing page.
3. Complete payment in live Stripe.
4. Confirm:
   - checkout returns to `/app/account/billing`
   - webhook is delivered successfully
   - `/api/auth/me` reflects Standard plan
   - account page shows active billing state

### Live Professional checkout
1. Repeat checkout for Professional.
2. Confirm:
   - plan updates to Professional
   - correct live price ID is persisted
   - `price-mapping-health` does not report the price as unknown

### Billing portal open
1. From billing/account page, open the billing portal.
2. Confirm portal session opens successfully.
3. Confirm return path lands back on the app.

### Renewal sync
1. Use a controlled live subscription that renews.
2. Confirm `invoice.paid` and/or `customer.subscription.updated` is processed.
3. Confirm entitlement remains paid and current.

### Cancellation at period end
1. Cancel a paid subscription from the app or portal.
2. Confirm:
   - `CancelAtPeriodEnd = true`
   - account UI shows scheduled cancellation clearly
   - access remains active until period end
3. After the end date, confirm downgrade to Free and `canceled` handling.

### Payment failure handling
1. Trigger or observe a live failure scenario on a controlled account.
2. Confirm:
   - `invoice.payment_failed` is logged
   - subscription status becomes `past_due` or `unpaid`
   - account UI shows access paused due to billing state
   - no silent downgrade due to unknown price mapping

### Admin resync
1. Call:
   - `POST /api/admin/stripe/resync`
2. Confirm:
   - Stripe subscription is fetched from live Stripe
   - entitlement sync succeeds
   - mode mismatch is not reported for the target user

## 7. Readiness Diagnostics

Use these admin endpoints during cutover:

### `GET /api/admin/stripe/readiness`

Checks:
- resolved Stripe mode
- secret key present
- webhook secret present
- active-mode Standard/Professional prices configured
- billing portal configured enough to operate
- recent webhook deliveries
- recent webhook errors
- recent sync outcomes
- mixed-mode billing records
- unknown Stripe price mappings

Readiness states:
- `ready`
- `warning`
- `blocked`

Typical blocked states:
- live mode with missing live price IDs
- missing live webhook secret
- Stripe disabled
- missing live secret key

Typical warning states:
- mixed-mode persisted billing records
- unknown Stripe price mappings
- recent webhook processing errors
- portal fallback configuration not explicit

### `GET /api/admin/stripe/price-mapping-health`

Checks:
- configured live/test prices
- known mapped prices
- entitlements tied to unknown Stripe price IDs

## 8. Cutover Sequence

Recommended order:

1. Prepare live Stripe products, prices, portal, and webhook.
2. Apply production environment variables.
3. Restart production.
4. Confirm startup logs show live mode and active-mode price presence.
5. Check `/api/admin/stripe/readiness`.
6. Check `/api/admin/stripe/price-mapping-health`.
7. Run controlled live Standard checkout.
8. Run controlled live Professional checkout.
9. Validate portal, cancellation, payment failure, and admin resync.
10. Enable customer traffic.

## 9. Rollback Guidance

If live billing is not healthy:

### Immediate rollback actions
1. Disable customer upgrade flow by setting:
   - `Stripe__Enabled=false`
2. Restart the app.
3. Leave existing customer data intact.
4. Continue investigating through:
   - startup logs
   - `/api/admin/stripe/readiness`
   - `/api/admin/stripe/price-mapping-health`
   - recent `StripeEventLogs`

### If webhook configuration is wrong
1. Correct the Stripe live webhook endpoint or signing secret.
2. Restart if app settings changed.
3. Use admin resync for affected users after webhook delivery is restored:
   - `POST /api/admin/stripe/resync`

### If live price mapping is wrong
1. Fix:
   - `Stripe__Prices__Standard__LivePriceId`
   - `Stripe__Prices__Pro__LivePriceId`
2. Restart the app.
3. Check `price-mapping-health`.
4. Resync affected users.

### If mixed test/live linkage is detected
1. Do not ignore it.
2. Identify affected users from readiness diagnostics.
3. Reset stale linkage carefully for those rows.
4. Re-sync the users against live Stripe only.

## 10. Do Not Do This

Do not:
- enable live mode without `Stripe__WebhookSecret`
- enable live mode without live Standard and Professional price IDs
- point production at Stripe live while still using a test secret key
- share test and live Stripe linkage in one DB as a normal operating model
- assume checkout success alone is enough without webhook confirmation
- ignore unknown live price mappings

## 11. Success Criteria

Cutover is complete when:
- `GET /api/admin/stripe/readiness` returns `ready`
- live Standard and Professional checkout both work
- billing portal opens correctly
- webhook deliveries are processing without errors
- no unknown live price mappings are reported
- no mixed-mode billing records remain for active live users
