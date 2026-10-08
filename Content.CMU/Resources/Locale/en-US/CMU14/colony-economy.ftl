# Shared colony economy status
colony-economy-sales-tax = Sales Tax: { $percent }%
colony-economy-income-tax = Income Tax: { $percent }%
colony-economy-transit-tariff = Transit Tariff: { $percent }%
colony-economy-active-embargoes = Active Embargoes: { $factions }
colony-economy-no-embargoes = Embargoes: None
colony-economy-active-trade-pacts = Active Trade Pacts: { $factions }
colony-economy-no-trade-pacts = Trade Pacts: None
colony-economy-overview = -- Colony Economy Overview --
colony-economy-third-party-support = -- Third Party Support --
colony-economy-open-third-party-menu = Open Third Party Menu
colony-economy-unknown-faction = Unknown Faction
colony-economy-apply = Apply

# Administration console
admin-console-title = Administration Console
admin-console-colony-budget = Colony Budget: ${ $amount }
admin-console-sales-tax-section = -- Sales Tax --
admin-console-current-sales-tax = Current Sales Tax: { $percent }%
admin-console-sales-tax-description = Applies to cash vendors and corporate ASRS orders. Tax revenue goes to the colony budget.
admin-console-new-tax = New Tax (0–50%):
admin-console-tax-placeholder = e.g. 10
admin-console-income-tax-section = -- Income Tax --
admin-console-current-income-tax = Current Income Tax: { $percent }%
admin-console-income-tax-description = Deducted from salary payouts and corporate cash withdrawals. Revenue goes to the colony budget.
admin-console-announcement-sender = Administration
admin-console-sales-tax-announcement = Colony sales tax has been set to { $percent }%.
admin-console-income-tax-announcement = Colony income tax has been set to { $percent }%. This affects salary payouts and corporate withdrawals.

# Corporate console
corporate-console-title = Corporate Affairs Console
corporate-console-budget = Corporate Budget: ${ $amount }
corporate-console-withdraw-section = -- Withdraw Cash --
corporate-console-transit-tariff-section = -- Transit Tariff --
corporate-console-current-transit-tariff = Current Transit Tariff: { $percent }%
corporate-console-transit-tariff-description = A percentage of all submission storage payouts that goes to the corporate budget instead of the colony.
corporate-console-new-tariff = New Tariff (0–50%):
corporate-console-tariff-placeholder = e.g. 15
corporate-console-withdraw-tax-note = Note: Withdrawals are subject to { $percent }% income tax.
corporate-console-withdraw-no-tax = No income tax on withdrawals.
corporate-console-announcement-sender = Corporate Affairs
corporate-console-tariff-announcement = Corporate transit tariff has been set to { $percent }%. Submission payouts to the colony have been adjusted.

# Budget console
budget-console-title = Budget Console
budget-console-current-budget = Current Budget: { $amount }
budget-console-withdraw-cash = Withdraw Cash:
budget-console-dispense-salaries = Dispense All Salaries
budget-console-transfer-department = Transfer to Department:
budget-console-amount-placeholder = Amount
budget-console-department-entry = { $department } (Budget: ${ $amount })
budget-console-transfer = Transfer
budget-console-no-departments = No departments found.

# Cash vendor
cash-vendor-credit = Credit:
cash-vendor-amount = ${ $amount }
cash-vendor-scan-id = Scan ID
cash-vendor-clear-department = Clear Dept
cash-vendor-return-change = Return Change
cash-vendor-department-budget = Dept Budget:
cash-vendor-department-budget-value = ${ $amount } ({ $department })
cash-vendor-search-placeholder = Search...
cash-vendor-footer-hint = Insert cash or carry your ID, then select item.
cash-vendor-prices-include-tax = Prices incl. tax
cash-vendor-id-account = ID Account:
cash-vendor-insufficient-cash = Not enough cash, and no ID card to charge the rest to.
cash-vendor-insufficient-funds = Insufficient funds on your ID card.
cash-vendor-card-locked = Your ID card is locked.
cash-vendor-card-charged = ${ $amount } charged to your ID card.
cash-vendor-sales-tax = Sales Tax: { $percent }%
cash-vendor-no-sales-tax = No sales tax
cash-vendor-buy = Buy
cash-vendor-no-items = No items available.

# Colony ATM card reader
cmu-atm-card-slot-occupied = There's already a card in the ATM.
cmu-atm-no-card = You have no ID card to put in.
cmu-atm-take-card-verb = Take card
cmu-atm-take-cash-verb = Take cash
cmu-atm-take-card-start = You start pulling the card out of the ATM...
cmu-atm-take-card-start-others = {CAPITALIZE(THE($user))} starts pulling a card out of the ATM!

# Colony ATM power-on self test, shown on the terminal as it boots
cmu-atm-boot-title = W-Y COLONY FINANCIAL SYSTEMS
cmu-atm-boot-bios = BIOS 2.7 (C) 2179 W-Y CORP.
cmu-atm-boot-memory = MEMORY 640K
cmu-atm-boot-keypad = KEYPAD
cmu-atm-boot-reader = CARD READER
cmu-atm-boot-dispenser = CASH DISPENSER
cmu-atm-boot-uplink = UN TREASURY UPLINK
cmu-atm-boot-ok = OK
cmu-atm-boot-loading = LOADING TERMINAL...

# Colony ATM knocked out by a sapper's siphon rig: a console gone wrong behind a plain notice.
# The second line gives way to whatever message the sapper left.
cmu-atm-out-of-order = OUT OF ORDER
cmu-atm-out-of-order-sorry = PLEASE USE ANOTHER MACHINE

# Colony ATM screen hints; the keys are labelled OK and X
cmu-atm-hint-confirm = OK = confirm   X = back
cmu-atm-hint-continue = OK to continue.
cmu-atm-hint-certificate = OK to continue. 1 = print certificate.
cmu-atm-hint-history = OK = back   1 = print

# Colony ATM nav bar
cmu-atm-nav-title = Colony ATM
cmu-atm-nav-pin = Your card #{ $account } - PIN { $pin }
cmu-atm-nav-no-card = You have no card of your own
cmu-atm-nav-pin-unknown = Reading your card...
cmu-atm-nav-pop-out = Pop Out

# Bank paperwork
cmu-bank-stamp-name = W-Y Colonial Bank
cmu-bank-slot-full = Take the paper waiting in the slot first.
cmu-bank-take-receipt-verb = Take receipt
cmu-bank-statement-name = account statement (#{ $account })
cmu-bank-statement-title = ACCOUNT STATEMENT
cmu-bank-statement-columns = DATE             AMOUNT  DETAILS
cmu-bank-statement-empty = No transactions yet.
cmu-bank-statement-footer = W-Y Colonial Bank. Keep this statement for your records.
cmu-bank-certificate-name = certificate of transfer ({ $reference })
cmu-bank-certificate-title = CERTIFICATE OF TRANSFER
cmu-bank-certificate-body = This certifies that { $amount } was transferred between the accounts below.
cmu-bank-certificate-footer = The reference is listed on both accounts' statements.
cmu-bank-receipt-name = card receipt ({ $total })
cmu-bank-receipt-title = CARD PAYMENT RECEIPT
cmu-bank-receipt-merchant-copy = MERCHANT COPY
cmu-bank-receipt-sale = SALE
cmu-bank-receipt-tip = TIP ({ $percent }%)
cmu-bank-receipt-total = TOTAL
cmu-bank-receipt-card = { $digits }, PIN verified
cmu-bank-receipt-approved = APPROVED
cmu-bank-field-holder = Account holder:
cmu-bank-field-account = Account:
cmu-bank-field-issued = Issued:
cmu-bank-field-balance = Balance:
cmu-bank-field-from = From:
cmu-bank-field-to = To:
cmu-bank-field-amount = Amount:
cmu-bank-field-date = Date:
cmu-bank-field-reference = Reference:
cmu-bank-field-merchant = Merchant:
cmu-bank-field-card = Card:
cmu-bank-line-withdrawal = WITHDRAWAL
cmu-bank-line-deposit = DEPOSIT
cmu-bank-line-cash-deposit = CASH DEPOSIT
cmu-bank-line-transfer-out = TO #{ $account }
cmu-bank-line-transfer-in = FROM #{ $account }
cmu-bank-line-retracted = CASH RETURNED
cmu-bank-line-purchase = PURCHASE
cmu-bank-line-card-payment = PAID #{ $account }
cmu-bank-line-card-sale = SALE #{ $account }
cmu-bank-receipt-copy-name = card receipt, merchant copy ({ $total })

# Card terminal
cmu-terminal-nav-title = Card Terminal
cmu-terminal-nav-customer = Card Payment
cmu-terminal-header = CARD TERMINAL
cmu-terminal-not-set-up = NOT SET UP
cmu-terminal-tap-to-register = Tap your card to register.
cmu-terminal-enter-pin = ENTER PIN
cmu-terminal-ready = READY
cmu-terminal-payout = Pays into #{ $account }
cmu-terminal-amount = Amount:
cmu-terminal-charge = CHARGE { $amount }
cmu-terminal-use-on-customer = Use the terminal on the customer.
cmu-terminal-waiting = WAITING FOR
cmu-terminal-approved = APPROVED
cmu-terminal-approved-from = { $amount } from { $name }
cmu-terminal-declined = DECLINED
cmu-terminal-hint-cancel = X = cancel
cmu-terminal-hint-new-sale = OK = new sale
cmu-terminal-hint-copy = 1 = merchant copy
cmu-terminal-setup = SETUP
cmu-terminal-tap-owner = Tap the owner's card.
cmu-terminal-setup-payout = 1) Payout #{ $account }
cmu-terminal-setup-tips = 2) Tips: { $state }
cmu-terminal-setup-presets = 3) Tip presets { $presets }%
cmu-terminal-setup-unregister = 4) Unregister
cmu-terminal-setup-done = X = done
cmu-terminal-on = ON
cmu-terminal-off = OFF
cmu-terminal-payout-title = PAYOUT ACCOUNT
cmu-terminal-account = Account #:
cmu-terminal-tip-title = TIP PRESET { $index } OF 3
cmu-terminal-percent = Percent:
cmu-terminal-pay-to = PAY { $name }
cmu-terminal-amount-line = Amount { $amount }
cmu-terminal-add-tip = ADD A TIP?
cmu-terminal-tip-option = { $key }) { $percent }%  { $amount }
cmu-terminal-no-tip = 0) No tip
cmu-terminal-total = TOTAL { $amount }
cmu-terminal-tap-card = TAP YOUR CARD
cmu-terminal-thanks = Thank you!
cmu-terminal-take-receipt = Take your receipt.
cmu-terminal-no-card = Hold or wear your ID card.
cmu-terminal-not-owner = Not the owner's card.
cmu-terminal-wrong-pin = Incorrect PIN.
cmu-terminal-registered = Registered to { $name }.
cmu-terminal-no-account = Account not found.
cmu-terminal-declined-locked = Card locked.
cmu-terminal-declined-funds = Insufficient funds.
cmu-terminal-declined-payout = Payout account not found.
cmu-terminal-declined-self = Can't pay your own account.
cmu-terminal-declined-card = Card not readable.
cmu-terminal-cancelled = Cancelled.
cmu-terminal-timed-out = Timed out.
cmu-terminal-popup-no-sale = Enter an amount first.
cmu-terminal-popup-busy = The terminal is waiting on another payment.
cmu-terminal-popup-present = { CAPITALIZE(THE($merchant)) } holds out a card terminal: { $amount }.
cmu-terminal-popup-present-self = You hold out the card terminal to { THE($customer) }.
