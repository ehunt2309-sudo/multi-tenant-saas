# Fills the demo venue with realistic data: experiences, staff, offers, boutique products, customers,
# a month of past bookings (so the manager dashboard has real numbers), upcoming bookings, shifts and site content.
# Safe to run more than once: it skips things that already exist.
param(
  [string]$Api = "https://localhost:7260",
  [string]$SiteHost = "localhost:5173",
  [string]$ManagerEmail = "manager@luxespa.example",
  [string]$ManagerPassword = "Granite-Orchid-Spa-7421"
)
$ErrorActionPreference = "Continue"
$j = "application/json"
Get-Random -SetSeed 7 | Out-Null

function Call($method, $path, $body) {
  try {
    $args = @{ Method = $method; Uri = "$Api$path"; Headers = $script:headers; ContentType = $j }
    if ($null -ne $body) { $args.Body = ($body | ConvertTo-Json -Depth 6) }
    return Invoke-RestMethod @args
  } catch {
    $m = $_.ErrorDetails.Message
    try { $p = $m | ConvertFrom-Json; $m = "$($p.title) - $($p.detail)" } catch {}
    Write-Host "  ! $method $path : $m" -ForegroundColor Yellow
    return $null
  }
}
function Iso($dt) { return $dt.ToUniversalTime().ToString("o") }

Write-Host "Signing in as $ManagerEmail ..." -ForegroundColor Cyan
$script:headers = @{ "X-Tenant-Host" = $SiteHost }
$login = Call Post "/api/auth/login" @{ email = $ManagerEmail; password = $ManagerPassword }
if (-not $login) { Write-Host "Could not sign in. Is the backend running and is the manager password right?" -ForegroundColor Red; exit 1 }
$script:headers = @{ Authorization = "Bearer $($login.accessToken)" }

# ---------- Experiences ----------
Write-Host "Experiences..." -ForegroundColor Cyan
$have = @{}; (Call Get "/api/services/all" $null) | ForEach-Object { $have[$_.name] = $true }
$services = @(
  @("Signature Massage", "Ninety minutes of deep restoration with warmed oils.", 90, 1200),
  @("Deep Tissue Release", "Targeted work for stubborn tension in back and shoulders.", 60, 950),
  @("Hot Stone Therapy", "Heated basalt stones melt away tension and quiet the mind.", 75, 1100),
  @("Radiance Facial", "A bespoke facial using rose quartz and botanical actives.", 60, 890),
  @("Couples Retreat", "Side-by-side massages in a private candle-lit suite, with tea ritual.", 120, 2600),
  @("Body Polish & Wrap", "Exfoliation, a mineral wrap and a scalp massage.", 90, 1350),
  @("Manicure & Pedicure", "Unhurried hand and foot care, finished to perfection.", 75, 640),
  @("Thermal Suite Day Pass", "Sauna, steam, plunge pool and relaxation lounge for the afternoon.", 240, 750)
)
foreach ($s in $services) {
  if (-not $have[$s[0]]) { Call Post "/api/services" @{ name = $s[0]; description = $s[1]; durationMinutes = $s[2]; price = $s[3]; isActive = $true } | Out-Null }
}

# ---------- Staff ----------
Write-Host "Staff..." -ForegroundColor Cyan
$haveStaff = @{}; (Call Get "/api/staff" $null) | ForEach-Object { $haveStaff[$_.email] = $true }
$team = @(
  @("anna@luxespa.example", "Lantern-Harbor-Tea-8802", "Anna", "Dlamini", "Senior Therapist", 180),
  @("isabel@luxespa.example", "Velvet-Cedar-Moon-5531", "Isabel", "Fourie", "Facial Specialist", 160),
  @("marcus@luxespa.example", "Copper-Willow-Dawn-6620", "Marcus", "Naidoo", "Sports Massage Therapist", 170),
  @("thandi@luxespa.example", "Silver-Fennel-Rain-4417", "Thandi", "Mokoena", "Wellness Therapist", 150),
  @("sofia@luxespa.example", "Amber-Linen-Coast-9073", "Sofia", "van Wyk", "Nail Artist", 140)
)
foreach ($t in $team) {
  if (-not $haveStaff[$t[0]]) { Call Post "/api/staff" @{ email = $t[0]; password = $t[1]; firstName = $t[2]; lastName = $t[3]; jobTitle = $t[4]; hourlyRate = $t[5]; role = "Staff" } | Out-Null }
}

# ---------- Offers ----------
Write-Host "Offers..." -ForegroundColor Cyan
$havePromo = @{}; (Call Get "/api/promotions?pageSize=100" $null).items | ForEach-Object { $havePromo[$_.title] = $true }
$start = Iso ((Get-Date).AddDays(-2)); $end30 = Iso ((Get-Date).AddDays(30)); $end60 = Iso ((Get-Date).AddDays(60)); $end14 = Iso ((Get-Date).AddDays(14))
$offers = @(
  @("Spring Renewal", "Fifteen percent off every treatment, all season.", "Percentage", 15, $end30),
  @("Couples Retreat Special", "R500 off our two-hour couples retreat.", "FixedAmount", 500, $end60),
  @("First Visit Ritual", "A warm welcome: 20% off your very first experience.", "Percentage", 20, $end60),
  @("Midweek Calm", "Slow down on a Monday to Wednesday and save 10%.", "Percentage", 10, $end30),
  @("Birthday Month Gift", "Enjoy a complimentary 30-minute scalp ritual in your birthday month.", "None", 0, $end60),
  @("Refer a Friend", "Introduce a friend and you each receive 500 reward points.", "None", 0, $end60),
  @("Bridal Party Package", "Groups of four or more enjoy 12% off, with champagne.", "Percentage", 12, $end60),
  @("Inner Circle Evenings", "Gold and Platinum members: private late openings, first Friday monthly.", "None", 0, $end14)
)
foreach ($o in $offers) {
  if (-not $havePromo[$o[0]]) { Call Post "/api/promotions" @{ title = $o[0]; description = $o[1]; discountType = $o[2]; discountValue = $o[3]; startsAt = $start; endsAt = $o[4]; isActive = $true } | Out-Null }
}

# ---------- Boutique ----------
Write-Host "Boutique..." -ForegroundColor Cyan
$haveProd = @{}; (Call Get "/api/products/all" $null).items | ForEach-Object { $haveProd[$_.name] = $true }
$products = @(
  @("Rose Quartz Facial Oil", "Weightless, luminous, deeply nourishing.", 890, 24),
  @("Himalayan Bath Salts", "Mineral-rich salts with lavender and cedar.", 320, 40),
  @("Hydrating Face Cream", "Daily comfort with hyaluronic acid.", 640, 18),
  @("Signature Candle", "Amber, fig and sandalwood. Sixty-hour burn.", 450, 30),
  @("Silk Eye Mask", "Pure mulberry silk. Total darkness, total calm.", 280, 4),
  @("Body Polish", "Sea salt and neroli scrub.", 380, 22),
  @("Aromatherapy Mist", "Pillow and room mist for deep rest.", 260, 35),
  @("Gift Voucher", "Give the gift of stillness. Redeemable on any experience.", 1000, 100)
)
foreach ($p in $products) {
  if (-not $haveProd[$p[0]]) { Call Post "/api/products" @{ name = $p[0]; description = $p[1]; price = $p[2]; stockQuantity = $p[3]; imageUrl = $null; isActive = $true } | Out-Null }
}

# ---------- Site content ----------
Write-Host "Website content..." -ForegroundColor Cyan
Call Put "/api/pages/home" @{ title = "Home"; isPublished = $true; content = @{
  tagline = "A sanctuary for the senses"
  about = "Step out of the everyday. Every treatment is tailored to you, every detail considered, every visit unhurried. Our practitioners bring decades of craft to rituals designed to restore body and mind."
  address = "12 Jacaranda Lane, Sandton, Johannesburg"
  phone = "+27 11 555 0142"
  email = "reservations@luxespa.example"
  hours = @("Monday to Friday: 09:00 - 18:00", "Saturday: 09:00 - 17:00", "Sunday: By appointment")
} } | Out-Null

# ---------- Customers ----------
Write-Host "Customers..." -ForegroundColor Cyan
$haveCust = @{}; $existing = (Call Get "/api/customers?pageSize=100" $null).items
$existing | ForEach-Object { if ($_.email) { $haveCust[$_.email] = $_.id } }
$people = @(
  @("Naledi", "Khumalo", "naledi.k@example.com", "1991-04-12", "Female"), @("James", "Pretorius", "james.p@example.com", "1978-09-03", "Male"),
  @("Ayesha", "Patel", "ayesha.p@example.com", "1985-01-22", "Female"), @("Liam", "O'Connor", "liam.o@example.com", "1996-07-30", "Male"),
  @("Zinhle", "Ndlovu", "zinhle.n@example.com", "1989-11-05", "Female"), @("Charlotte", "Botha", "charlotte.b@example.com", "1966-02-17", "Female"),
  @("Thabo", "Mahlangu", "thabo.m@example.com", "1982-06-25", "Male"), @("Megan", "Williams", "megan.w@example.com", "2000-12-09", "Female"),
  @("Riaan", "Steyn", "riaan.s@example.com", "1974-08-14", "Male"), @("Priya", "Naidoo", "priya.n@example.com", "1993-03-28", "Female"),
  @("Dineo", "Sithole", "dineo.s@example.com", "1998-10-19", "Non-binary"), @("Annelize", "Kruger", "annelize.k@example.com", "1959-05-07", "Female")
)
foreach ($c in $people) {
  if (-not $haveCust[$c[2]]) {
    $r = Call Post "/api/customers" @{ firstName = $c[0]; lastName = $c[1]; email = $c[2]; phone = "+2782$(Get-Random -Min 1000000 -Max 9999999)"; dateOfBirth = $c[3]; gender = $c[4]; marketingConsent = $true }
    if ($r) { $haveCust[$c[2]] = $r.id }
  }
}
$customerIds = @($haveCust.Values)

# ---------- Bookings (past, completed -> real analytics; plus upcoming) ----------
$svc = @(Call Get "/api/services/all" $null | Where-Object { $_.isActive })
$staffList = @(Call Get "/api/staff" $null | Where-Object { $_.role -eq "Staff" -and $_.isActive })
$already = (Call Get "/api/bookings?from=$([uri]::EscapeDataString((Iso (Get-Date).AddDays(-45))))&to=$([uri]::EscapeDataString((Iso (Get-Date).AddDays(30))))&pageSize=100" $null).total
if ($already -lt 10 -and $customerIds.Count -gt 0 -and $svc.Count -gt 0 -and $staffList.Count -gt 0) {
  Write-Host "Bookings (about 45 past, 8 upcoming)..." -ForegroundColor Cyan
  $ok = 0
  for ($i = 0; $i -lt 45; $i++) {
    $day = (Get-Date).ToUniversalTime().Date.AddDays(-(Get-Random -Min 1 -Max 29))
    $when = $day.AddHours((Get-Random -Min 7 -Max 14)).AddMinutes((Get-Random -InputObject @(0, 30)))
    $b = Call Post "/api/bookings" @{ customerId = (Get-Random -InputObject $customerIds); serviceOfferingId = (Get-Random -InputObject $svc).id; staffMemberId = (Get-Random -InputObject $staffList).id; startsAt = (Iso $when); partySize = 1 }
    if ($b) {
      $roll = Get-Random -Min 1 -Max 101
      $final = if ($roll -le 80) { "Completed" } elseif ($roll -le 88) { "NoShow" } else { "Cancelled" }
      Call Patch "/api/bookings/$($b.id)/status" @{ status = $final } | Out-Null
      $ok++
    }
  }
  for ($i = 0; $i -lt 8; $i++) {
    $day = (Get-Date).ToUniversalTime().Date.AddDays((Get-Random -Min 1 -Max 12))
    $when = $day.AddHours((Get-Random -Min 7 -Max 14)).AddMinutes((Get-Random -InputObject @(0, 30)))
    Call Post "/api/bookings" @{ customerId = (Get-Random -InputObject $customerIds); serviceOfferingId = (Get-Random -InputObject $svc).id; staffMemberId = (Get-Random -InputObject $staffList).id; startsAt = (Iso $when); partySize = (Get-Random -Min 1 -Max 3) } | Out-Null
  }
  Write-Host "  created $ok past bookings (clashes are skipped automatically)"
} else { Write-Host "Bookings already present, skipping." -ForegroundColor DarkGray }

# ---------- Shifts (this week + next). The first staff member is ON shift right now so you can try Clock in. ----------
Write-Host "Shifts..." -ForegroundColor Cyan
$haveShifts = @(Call Get "/api/shifts?from=$([uri]::EscapeDataString((Iso (Get-Date).AddDays(-4))))&to=$([uri]::EscapeDataString((Iso (Get-Date).AddDays(10))))" $null).Count
if ($haveShifts -eq 0) {
  $today = (Get-Date).ToUniversalTime().Date
  foreach ($m in $staffList) {
    for ($d = -3; $d -le 10; $d++) {
      $day = $today.AddDays($d)
      if ($day.DayOfWeek -eq "Sunday") { continue }
      if ($d -eq 0 -and $m.id -eq $staffList[0].id) { $s = (Get-Date).ToUniversalTime().AddHours(-1); $e = (Get-Date).ToUniversalTime().AddHours(7) }
      else { $s = $day.AddHours(7); $e = $day.AddHours(15) }
      Call Post "/api/shifts" @{ staffMemberId = $m.id; startsAt = (Iso $s); endsAt = (Iso $e); notes = $null } | Out-Null
    }
  }
} else { Write-Host "Shifts already present, skipping." -ForegroundColor DarkGray }

Write-Host "`nDone. Refresh http://localhost:5173" -ForegroundColor Green
