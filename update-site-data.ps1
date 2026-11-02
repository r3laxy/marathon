# Updates the 3 restaurant locations to match the design and adds sample customer reviews.
# Safe to run more than once (reviews use fixed ids, so they are overwritten, not duplicated).
$ErrorActionPreference = 'Stop'
$region = 'eu-north-1'
$tmp = Join-Path $env:TEMP 'site-data'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$utf8 = New-Object System.Text.UTF8Encoding $false

function Write-Json($name, $obj) {
  $path = Join-Path $tmp $name
  [System.IO.File]::WriteAllText($path, ($obj | ConvertTo-Json -Depth 20 -Compress), $utf8)
  return 'file://' + ($path -replace '\\', '/')
}

$tables = (aws dynamodb list-tables --region $region --output json | ConvertFrom-Json).TableNames
$locationsTable = $tables | Where-Object { $_ -match 'Locations' -and $_ -notmatch 'Report' } | Sort-Object Length | Select-Object -First 1
$feedbackTable = $tables | Where-Object { $_ -match 'Feedback' } | Sort-Object Length | Select-Object -First 1
if (-not $locationsTable -or -not $feedbackTable) { throw "Could not find the Locations / Feedback tables. Tables: $($tables -join ', ')" }
Write-Host "Locations table: $locationsTable"
Write-Host "Feedback table:  $feedbackTable"

$designs = @(
  @{ address = '48 Rustaveli Avenue'; occupancy = '90%'; rating = 4.73; image = '/assets/img/locations/rustaveli-avenue-48.jpg';
     description = 'Located on bustling Rustaveli Avenue, this branch offers a perfect mix of city energy and a cozy atmosphere. With its spacious outdoor terrace, this location is ideal for both casual lunches and intimate dinners.' },
  @{ address = '14 Baratashvili Street'; occupancy = '75%'; rating = 4.58; image = '/assets/img/locations/baratashvili-street-14.jpg';
     description = 'Tucked away on Baratashvili Street, this branch blends a lively open kitchen with a bright, plant-filled dining room. It is a favourite for relaxed weekend brunches and after-work dinners with friends.' },
  @{ address = '9 Abashidze Street'; occupancy = '80%'; rating = 4.65; image = '/assets/img/locations/abashidze-street-9.jpg';
     description = 'On quiet Abashidze Street, this branch is a calm, leafy retreat with warm lighting and a cozy window-side bar. It is perfect for slow family lunches and small celebrations.' }
)

$existing = @((aws dynamodb scan --table-name $locationsTable --region $region --output json | ConvertFrom-Json).Items | Sort-Object { $_.id.S })
$locationIds = @()

for ($index = 0; $index -lt 3; $index++) {
  $design = $designs[$index]
  if ($index -lt $existing.Count) {
    $locationId = $existing[$index].id.S
    $key = Write-Json "key$index.json" @{ id = @{ S = $locationId } }
    $names = Write-Json "names$index.json" @{ '#a' = 'address'; '#d' = 'description'; '#o' = 'averageOccupancy'; '#r' = 'rating'; '#i' = 'imageUrl' }
    $values = Write-Json "values$index.json" @{
      ':a' = @{ S = $design.address }; ':d' = @{ S = $design.description }; ':o' = @{ S = $design.occupancy };
      ':r' = @{ N = "$($design.rating)" }; ':i' = @{ S = $design.image } }
    aws dynamodb update-item --table-name $locationsTable --region $region --key $key `
      --update-expression 'SET #a = :a, #d = :d, #o = :o, #r = :r, #i = :i' `
      --expression-attribute-names $names --expression-attribute-values $values | Out-Null
    Write-Host "Updated location $locationId -> $($design.address)"
  } else {
    $locationId = [guid]::NewGuid().ToString()
    $item = Write-Json "item$index.json" @{
      id = @{ S = $locationId }; address = @{ S = $design.address }; description = @{ S = $design.description };
      totalCapacity = @{ N = '0' }; averageOccupancy = @{ S = $design.occupancy }; rating = @{ N = "$($design.rating)" };
      imageUrl = @{ S = $design.image } }
    aws dynamodb put-item --table-name $locationsTable --region $region --item $item | Out-Null
    Write-Host "Created location $locationId -> $($design.address) (no tables yet)"
  }
  $locationIds += $locationId
}

$serviceComments = @(
  'The staff was incredibly friendly and attentive, making our experience even better.',
  'Quick seating and a very helpful waiter who explained the whole vegan menu.',
  'Service was warm and professional, and our dishes arrived exactly on time.',
  'Our waiter remembered every detail of our order. Wonderful hospitality!',
  'Friendly team, but we waited a little for the bill.',
  'Great service overall. The manager even checked that we enjoyed the dessert.',
  'Polite and fast service, a lovely place for a business lunch.',
  'Attentive staff who happily adjusted the dishes for my allergy.'
)
$cuisineComments = @(
  'Absolutely loved this restaurant! The menu had so many fresh, healthy options, and the plant-based dishes had authentic Georgian flavors. Definitely coming back soon!',
  'The best dining experience I have had in Tbilisi. The vegan options were fantastic, and the flavors in every dish were so rich and authentic.',
  'Great food and an excellent vibe! The blend of traditional Georgian dishes with a modern twist was really impressive. Highly recommend the seasonal specials!',
  'I visited with friends and was blown away by the creativity of the menu. Each dish was a beautiful blend of traditional Georgian flavors and modern flair.',
  'The seasonal bowls are fresh and full of flavor. The strawberry mint salad is a must try.',
  'Wonderful plant-based dishes with authentic Georgian spices. Portions were generous.',
  'Lovely lemonade and avocado toast, everything tasted homemade.',
  'A little salty for my taste, but the presentation was beautiful and the ingredients clearly fresh.'
)
$ratings = @(5, 5, 4, 5, 4, 5, 3, 5)
$baseDate = [datetime]::SpecifyKind([datetime]'2026-10-20T12:00:00', 'Utc')

for ($index = 0; $index -lt 3; $index++) {
  $requests = @()
  for ($n = 0; $n -lt 8; $n++) {
    foreach ($type in 0, 1) {
      $comments = if ($type -eq 0) { $serviceComments } else { $cuisineComments }
      $created = $baseDate.AddDays(-($n * 9 + $index * 2 + $type)).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")
      $item = @{
        reservationId = @{ S = "seed-review-$index-$type-$n" }
        createdAt = @{ S = $created }
        locationId = @{ S = $locationIds[$index] }
        comment = @{ S = $comments[$n] }
        rating = @{ N = "$($ratings[($n + $index + $type) % 8])" }
        type = @{ N = "$type" }
      }
      $requests += @{ PutRequest = @{ Item = $item } }
    }
  }
  $batch = Write-Json "reviews$index.json" @{ $feedbackTable = $requests }
  aws dynamodb batch-write-item --region $region --request-items $batch | Out-Null
  Write-Host "Added $($requests.Count) reviews to $($designs[$index].address)"
}

Write-Host 'Done. Refresh the site (Ctrl+F5).'
