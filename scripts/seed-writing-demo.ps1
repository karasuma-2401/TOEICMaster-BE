param(
    [string]$BaseUrl = 'http://localhost:5287',
    [Parameter(Mandatory = $true)][pscredential]$Credential
)

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$auth = Invoke-RestMethod -Method Post -Uri "$base/api/v1/auth/login" -ContentType 'application/json' -Body (@{
    email = $Credential.UserName
    password = $Credential.GetNetworkCredential().Password
} | ConvertTo-Json)
$headers = @{ Authorization = "Bearer $($auth.accessToken)" }
$admin = "$base/api/v1/admin/writing"

function Invoke-SeedPost($path, $body) {
    Invoke-RestMethod -Method Post -Uri "$admin/$path" -Headers $headers -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 20) -TimeoutSec 60
}

function Find-SeedItem($collection, $code) {
    $page = Invoke-RestMethod -Uri "$admin/${collection}?query=$code&pageNumber=1&pageSize=20" -Headers $headers -TimeoutSec 60
    @($page.items | Where-Object { $_.code -eq $code }) | Select-Object -First 1
}

$testCode = 'DEMO-WRITING-001'
$existingTest = Find-SeedItem 'tests' $testCode
$existingExercises = @{}
for ($i = 1; $i -le 8; $i++) {
    $code = 'DEMO-WRITING-EX-{0:D2}' -f $i
    $existingExercises[$code] = Find-SeedItem 'exercises' $code
}

if ($existingTest -and $existingTest.status -eq 'PUBLISHED' -and @($existingExercises.Values | Where-Object { $null -eq $_ }).Count -eq 0) {
    Write-Host 'Dữ liệu Writing mẫu đã tồn tại; không tạo trùng.'
    return
}

# One deliberately simple, original illustration, reused for the related Part 1 prompts.
$imageId = @($existingExercises.Values | Where-Object { $_ -and $_.part -eq 1 -and $_.imageAssetId } | Select-Object -First 1).imageAssetId
if (-not $imageId) {
    Add-Type -AssemblyName System.Drawing
    $image = [System.Drawing.Bitmap]::new(800, 500)
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    $path = Join-Path ([System.IO.Path]::GetTempPath()) ("writing-demo-" + [guid]::NewGuid() + '.png')
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::FromArgb(235, 244, 248))
        $graphics.FillRectangle([System.Drawing.Brushes]::Tan, 0, 390, 800, 110)
        $graphics.FillRectangle([System.Drawing.Brushes]::SaddleBrown, 120, 300, 570, 35)
        $graphics.FillRectangle([System.Drawing.Brushes]::SaddleBrown, 155, 335, 30, 140)
        $graphics.FillRectangle([System.Drawing.Brushes]::SaddleBrown, 625, 335, 30, 140)
        $graphics.FillRectangle([System.Drawing.Brushes]::SlateGray, 455, 165, 180, 125)
        $graphics.FillRectangle([System.Drawing.Brushes]::LightBlue, 465, 175, 160, 105)
        $graphics.FillRectangle([System.Drawing.Brushes]::SlateGray, 532, 290, 26, 10)
        $graphics.FillEllipse([System.Drawing.Brushes]::BurlyWood, 275, 105, 95, 105)
        $graphics.FillEllipse([System.Drawing.Brushes]::SteelBlue, 245, 205, 155, 140)
        $graphics.DrawLine([System.Drawing.Pens]::Black, 340, 255, 460, 305)
        $graphics.DrawLine([System.Drawing.Pens]::Black, 315, 330, 320, 440)
        $graphics.DrawLine([System.Drawing.Pens]::Black, 360, 330, 410, 440)
        $image.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
        $client = [System.Net.Http.HttpClient]::new()
        $form = [System.Net.Http.MultipartFormDataContent]::new()
        try {
            $client.DefaultRequestHeaders.Authorization = [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $auth.accessToken)
            $fileContent = [System.Net.Http.ByteArrayContent]::new([System.IO.File]::ReadAllBytes($path))
            $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse('image/png')
            $form.Add($fileContent, 'file', 'writing-demo.png')
            $response = $client.PostAsync("$admin/media/images", $form).GetAwaiter().GetResult()
            $response.EnsureSuccessStatusCode() | Out-Null
            $imageId = ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json).id
        } finally {
            $form.Dispose()
            $client.Dispose()
        }
    } finally {
        $graphics.Dispose()
        $image.Dispose()
        Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue
    }
}

$pairs = @(
    @('person', 'desk'), @('work', 'computer'), @('sit', 'table'),
    @('screen', 'desk'), @('person', 'computer')
)
$inputs = @()
for ($i = 1; $i -le 8; $i++) {
    $part = if ($i -le 5) { 1 } elseif ($i -le 7) { 2 } else { 3 }
    $input = @{
        part = $part; code = ('DEMO-WRITING-EX-{0:D2}' -f $i); topic = 'Demo - Workplace'
        difficulty = 'MEDIUM'; status = 'PUBLISHED'; rubricNotes = ''; modelAnswer = ''
    }
    if ($part -eq 1) {
        $input.imageAssetId = $imageId
        $input.imageAlt = 'An illustration of a person working at a desk with a computer.'
        $input.keywordOne = $pairs[$i - 1][0]
        $input.keywordTwo = $pairs[$i - 1][1]
    } elseif ($part -eq 2) {
        $input.sender = if ($i -eq 6) { 'Morgan Lee' } else { 'Alex Chen' }
        $input.recipient = 'Office coordinator'
        $input.subject = if ($i -eq 6) { 'Planning a team meeting' } else { 'Training workshop' }
        $input.emailBody = if ($i -eq 6) { 'Our team needs to meet next week. Could you help us choose a time and a room?' } else { 'I am interested in the training workshop next month. Please send me more information.' }
        $input.directions = if ($i -eq 6) { 'Reply to the email. Suggest a meeting time and ask two questions about the meeting.' } else { 'Reply to the email. Provide two details about the workshop and ask one question.' }
    } else {
        $input.prompt = 'Do you agree or disagree that employees work more effectively when they can choose where they work? Give reasons and examples to support your opinion.'
        $input.minimumWordCount = 300
        $input.outline = ''
    }
    $inputs += $input
    if (-not $existingExercises[$input.code]) {
        $created = Invoke-SeedPost 'exercises' $input
        Write-Host "Đã tạo bài tập $($created.code)."
    } else {
        Write-Host "Đã có bài tập $($input.code); giữ nguyên."
    }
}

if (-not $existingTest) {
    $questions = for ($i = 0; $i -lt $inputs.Count; $i++) {
        $content = $inputs[$i].Clone()
        $content.code = 'DEMO-WRITING-TEST-Q{0:D2}' -f ($i + 1)
        $sequence = if ($i -lt 5) { $i + 1 } elseif ($i -lt 7) { $i - 4 } else { 1 }
        @{ part = $content.part; sequenceNumber = $sequence; exerciseId = $null; content = $content }
    }
    $createdTest = Invoke-SeedPost 'tests' @{
        code = $testCode; title = 'Đề Writing mẫu - Workplace'
        description = 'Dữ liệu minh họa để kiểm thử; không phải đề thi chính thức.'; questions = @($questions)
    }
    Write-Host "Đã tạo đề nháp $($createdTest.code)."
    $existingTest = $createdTest
}
if ($existingTest.status -eq 'DRAFT') {
    $published = Invoke-SeedPost "tests/$($existingTest.id)/publish" @{ version = $existingTest.version }
    Write-Host "Đã công bố đề $($published.code)."
} else {
    Write-Host "Đã có đề $testCode; giữ nguyên."
}
