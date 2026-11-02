# Adds / updates the 8 menu dishes to match the design. Safe to run more than once
# (a dish that already exists with the same name is updated instead of duplicated).
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
$dishesTable = $tables | Where-Object { $_ -match 'Dishes' } | Sort-Object Length | Select-Object -First 1
$locationsTable = $tables | Where-Object { $_ -match 'Locations' -and $_ -notmatch 'Report' } | Sort-Object Length | Select-Object -First 1
if (-not $dishesTable -or -not $locationsTable) { throw "Could not find the Dishes / Locations tables. Tables: $($tables -join ', ')" }
Write-Host "Dishes table: $dishesTable"

$locations = @((aws dynamodb scan --table-name $locationsTable --region $region --output json | ConvertFrom-Json).Items)
$rustaveli = $locations | Where-Object { $_.address.S -eq '48 Rustaveli Avenue' } | Select-Object -First 1
if (-not $rustaveli) { $rustaveli = $locations | Select-Object -First 1 }
$locationId = $rustaveli.id.S

# dishType: 0 = Appetizer, 1 = MainCourse, 2 = Dessert.  state: 1 = Available, 2 = OutOfStock
$menu = @(
  @{ name = 'Fresh Strawberry Mint Salad'; slug = 'fresh-strawberry-mint-salad'; type = 0; price = 17; weight = '430 g'; state = 1; calories = '~503'; proteins = '18-20g'; fats = '33-35'; carbs = '30-35g'; vitamins = 'Vit C, Vit A, Calcium, and Ferum';
     description = 'Strawberries, spinach, mint leaves, feta or goat cheese, walnuts, balsamic glaze, poppy seeds.' },
  @{ name = 'Green Asparagus Salad'; slug = 'green-asparagus-salad'; type = 0; price = 12; weight = '270 g'; state = 2; calories = '~310'; proteins = '12-14g'; fats = '18-22'; carbs = '16-20g'; vitamins = 'Vit K, Vit C, Folate, and Iron';
     description = 'Grilled green asparagus, mixed greens, shaved parmesan, lemon zest and a light olive oil dressing.' },
  @{ name = 'Roasted Sweet Potato & Lentil Salad'; slug = 'roasted-sweet-potato-and-lentil-salad'; type = 0; price = 10; weight = '380 g'; state = 1; calories = '~420'; proteins = '16-18g'; fats = '14-18'; carbs = '52-58g'; vitamins = 'Vit A, Vit C, Potassium, and Iron';
     description = 'Roasted sweet potato, green lentils, avocado, radicchio, red onion and fresh herbs with a tahini dressing.' },
  @{ name = 'Spring Salad'; slug = 'spring-salad'; type = 0; price = 19; weight = '280 g'; state = 1; calories = '~380'; proteins = '14-16g'; fats = '16-20'; carbs = '40-46g'; vitamins = 'Vit C, Vit K, Folate, and Magnesium';
     description = 'Quinoa, snap peas, roasted chickpeas, avocado, red onion and sweet peppers with a citrus dressing.' },
  @{ name = 'Chocolate Berry Bowl'; slug = 'chocolate-berry-bowl'; type = 0; price = 14; weight = '250 g'; state = 1; calories = '~340'; proteins = '8-10g'; fats = '18-22'; carbs = '34-40g'; vitamins = 'Vit C, Magnesium, Fiber, and Iron';
     description = 'Chocolate chia pudding topped with fresh raspberries, strawberry, cacao nibs and edible flowers.' },
  @{ name = 'Avocado Pine Nut Bowl'; slug = 'avocado-pine-nut-bowl'; type = 0; price = 17; weight = '430 g'; state = 1; calories = '~470'; proteins = '12-14g'; fats = '32-36'; carbs = '24-28g'; vitamins = 'Vit E, Vit K, Potassium, and Magnesium';
     description = 'Cucumber, avocado, cherry tomatoes, spinach, toasted pine nuts and seeds with a lemon dressing.' },
  @{ name = 'Avocado Egg Toast'; slug = 'avocado-egg-toast'; type = 0; price = 13; weight = '220 g'; state = 1; calories = '~390'; proteins = '16-18g'; fats = '22-26'; carbs = '28-32g'; vitamins = 'Vit B12, Vit D, Folate, and Iron';
     description = 'Whole grain toast with avocado, soft-boiled egg, baby spinach and fresh rosemary.' },
  @{ name = 'Orange Blackberry Tart'; slug = 'orange-blackberry-tart'; type = 0; price = 11; weight = '180 g'; state = 1; calories = '~360'; proteins = '5-7g'; fats = '20-24'; carbs = '38-44g'; vitamins = 'Vit C, Vit A, Calcium, and Fiber';
     description = 'A crisp nut crust filled with vanilla cream, fresh orange slices, blackberries and rosemary.' }
)

$existing = @((aws dynamodb scan --table-name $dishesTable --region $region --output json | ConvertFrom-Json).Items)

foreach ($dish in $menu) {
  $found = $existing | Where-Object { $_.name.S -eq $dish.name } | Select-Object -First 1
  $image = "/assets/img/dishes/menu/$($dish.slug).png"
  if ($found) {
    $key = Write-Json 'dkey.json' @{ id = @{ S = $found.id.S } }
    $names = Write-Json 'dnames.json' @{ '#d' = 'description'; '#t' = 'dishType'; '#p' = 'price'; '#w' = 'weight'; '#c' = 'calories'; '#pr' = 'proteins'; '#f' = 'fats'; '#cb' = 'carbohydrates'; '#v' = 'vitamins'; '#i' = 'imageUrl'; '#s' = 'state' }
    $values = Write-Json 'dvalues.json' @{
      ':d' = @{ S = $dish.description }; ':t' = @{ N = "$($dish.type)" }; ':p' = @{ N = "$($dish.price)" }; ':w' = @{ S = $dish.weight };
      ':c' = @{ S = $dish.calories }; ':pr' = @{ S = $dish.proteins }; ':f' = @{ S = $dish.fats }; ':cb' = @{ S = $dish.carbs };
      ':v' = @{ S = $dish.vitamins }; ':i' = @{ S = $image }; ':s' = @{ N = "$($dish.state)" } }
    aws dynamodb update-item --table-name $dishesTable --region $region --key $key `
      --update-expression 'SET #d = :d, #t = :t, #p = :p, #w = :w, #c = :c, #pr = :pr, #f = :f, #cb = :cb, #v = :v, #i = :i, #s = :s' `
      --expression-attribute-names $names --expression-attribute-values $values | Out-Null
    Write-Host "Updated: $($dish.name)"
  } else {
    $item = Write-Json 'ditem.json' @{
      id = @{ S = [guid]::NewGuid().ToString() }; name = @{ S = $dish.name }; description = @{ S = $dish.description };
      dishType = @{ N = "$($dish.type)" }; price = @{ N = "$($dish.price)" }; weight = @{ S = $dish.weight };
      calories = @{ S = $dish.calories }; proteins = @{ S = $dish.proteins }; fats = @{ S = $dish.fats };
      carbohydrates = @{ S = $dish.carbs }; vitamins = @{ S = $dish.vitamins }; imageUrl = @{ S = $image };
      state = @{ N = "$($dish.state)" }; locationId = @{ S = $locationId } }
    aws dynamodb put-item --table-name $dishesTable --region $region --item $item | Out-Null
    Write-Host "Added:   $($dish.name)"
  }
}

# Remove the old placeholder dishes that are not part of the design.
foreach ($oldName in @('Caesar Salad', 'Grilled Salmon', 'Tiramisu', 'Tom Yum')) {
  $old = $existing | Where-Object { $_.name.S -eq $oldName }
  foreach ($o in $old) {
    $key = Write-Json 'dkey.json' @{ id = @{ S = $o.id.S } }
    aws dynamodb delete-item --table-name $dishesTable --region $region --key $key | Out-Null
    Write-Host "Removed: $oldName"
  }
}

Write-Host 'Done. Refresh the site (Ctrl+F5).'
