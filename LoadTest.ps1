# Load Testing Script for URL Shortener System
# Requires PowerShell 7+ and curl

param(
    [string]$BaseUrl = "https://localhost:7000",
    [int]$ConcurrentUsers = 10,
    [int]$RequestsPerUser = 50,
    [int]$DurationSeconds = 60
)

# Allow self-signed SSL certificates for local testing
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

Write-Host "Starting Load Test" -ForegroundColor Green
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host "Concurrent Users: $ConcurrentUsers" -ForegroundColor Yellow
Write-Host "Requests per User: $RequestsPerUser" -ForegroundColor Yellow
Write-Host "Duration: $DurationSeconds seconds" -ForegroundColor Yellow
Write-Host ""

# Test data
$testUrls = @(
    "https://example.com/page1",
    "https://google.com/search?q=test",
    "https://github.com/microsoft/dotnet",
    "https://stackoverflow.com/questions/tagged/c%23",
    "https://docs.microsoft.com/en-us/dotnet/",
    "https://www.reddit.com/r/programming",
    "https://news.ycombinator.com",
    "https://medium.com/@developer",
    "https://dev.to/programming",
    "https://www.youtube.com/watch?v=dQw4w9WgXcQ"
)

# Results tracking
$global:results = [System.Collections.Concurrent.ConcurrentBag[object]]::new()
$global:errors = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
$global:startTime = Get-Date

function Test-CreateLink {
    param($url, $userId)
    
    $body = @{
        originalUrl = $url
        expirationDate = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ")
    } | ConvertTo-Json
    
    $startTime = Get-Date
    
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/api/links" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 30
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalMilliseconds
        
        $result = [PSCustomObject]@{
            UserId = $userId
            Operation = "CreateLink"
            Success = $true
            Duration = $duration
            StatusCode = 200
            ShortCode = $response.result.shortCode
            Timestamp = $startTime
        }
        
        $global:results.Add($result)
        return $response.result.shortCode
    }
    catch {
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalMilliseconds
        
        $result = [PSCustomObject]@{
            UserId = $userId
            Operation = "CreateLink"
            Success = $false
            Duration = $duration
            StatusCode = $_.Exception.Response.StatusCode.value__
            Error = $_.Exception.Message
            Timestamp = $startTime
        }
        
        $global:results.Add($result)
        $global:errors.Add("User $userId - CreateLink: $($_.Exception.Message)")
        return $null
    }
}

function Test-AccessLink {
    param($shortCode, $userId)
    
    if (-not $shortCode) { return }
    
    $startTime = Get-Date
    
    try {
        $response = Invoke-WebRequest -Uri "$BaseUrl/$shortCode" -Method GET -MaximumRedirection 0 -TimeoutSec 30 -ErrorAction SilentlyContinue
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalMilliseconds
        
        $success = $response.StatusCode -eq 302 -or $response.StatusCode -eq 301
        
        $result = [PSCustomObject]@{
            UserId = $userId
            Operation = "AccessLink"
            Success = $success
            Duration = $duration
            StatusCode = $response.StatusCode
            ShortCode = $shortCode
            Timestamp = $startTime
        }
        
        $global:results.Add($result)
    }
    catch {
        $endTime = Get-Date
        $duration = ($endTime - $startTime).TotalMilliseconds
        
        $result = [PSCustomObject]@{
            UserId = $userId
            Operation = "AccessLink"
            Success = $false
            Duration = $duration
            StatusCode = $_.Exception.Response.StatusCode.value__
            Error = $_.Exception.Message
            ShortCode = $shortCode
            Timestamp = $startTime
        }
        
        $global:results.Add($result)
        $global:errors.Add("User $userId - AccessLink: $($_.Exception.Message)")
    }
}

# Start load test
Write-Host "Starting concurrent users..." -ForegroundColor Magenta

$jobs = @()
for ($i = 1; $i -le $ConcurrentUsers; $i++) {
    $job = Start-Job -ScriptBlock {
        param($userId, $requestsPerUser, $BaseUrl, $DurationSeconds, $testUrls)
        
        # Allow self-signed SSL certificates for local testing
        [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
        
        function Test-CreateLink {
            param($url, $userId)
            
            $body = @{
                originalUrl = $url
                expirationDate = (Get-Date).AddDays(7).ToString("yyyy-MM-ddTHH:mm:ssZ")
            } | ConvertTo-Json
            
            $startTime = Get-Date
            
            try {
                $response = Invoke-RestMethod -Uri "$BaseUrl/api/links" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 30
                $endTime = Get-Date
                $duration = ($endTime - $startTime).TotalMilliseconds
                
                $result = [PSCustomObject]@{
                    UserId = $userId
                    Operation = "CreateLink"
                    Success = $true
                    Duration = $duration
                    StatusCode = 200
                    ShortCode = $response.result.shortCode
                    Timestamp = $startTime
                }
                
                return @{
                    Result = $result
                    ShortCode = $response.result.shortCode
                }
            }
            catch {
                $endTime = Get-Date
                $duration = ($endTime - $startTime).TotalMilliseconds
                
                $result = [PSCustomObject]@{
                    UserId = $userId
                    Operation = "CreateLink"
                    Success = $false
                    Duration = $duration
                    StatusCode = $_.Exception.Response.StatusCode.value__
                    Error = $_.Exception.Message
                    Timestamp = $startTime
                }
                
                return @{
                    Result = $result
                    ShortCode = $null
                    Error = $_.Exception.Message
                }
            }
        }
        
        function Test-AccessLink {
            param($shortCode, $userId)
            
            if (-not $shortCode) { return $null }
            
            $startTime = Get-Date
            
            try {
                $response = Invoke-WebRequest -Uri "$BaseUrl/$shortCode" -Method GET -MaximumRedirection 0 -TimeoutSec 30 -ErrorAction SilentlyContinue
                $endTime = Get-Date
                $duration = ($endTime - $startTime).TotalMilliseconds
                
                $success = $response.StatusCode -eq 302 -or $response.StatusCode -eq 301
                
                $result = [PSCustomObject]@{
                    UserId = $userId
                    Operation = "AccessLink"
                    Success = $success
                    Duration = $duration
                    StatusCode = $response.StatusCode
                    ShortCode = $shortCode
                    Timestamp = $startTime
                }
                
                return $result
            }
            catch {
                $endTime = Get-Date
                $duration = ($endTime - $startTime).TotalMilliseconds
                
                $result = [PSCustomObject]@{
                    UserId = $userId
                    Operation = "AccessLink"
                    Success = $false
                    Duration = $duration
                    StatusCode = $_.Exception.Response.StatusCode.value__
                    Error = $_.Exception.Message
                    ShortCode = $shortCode
                    Timestamp = $startTime
                }
                
                return $result
            }
        }
        
        # Simulate user behavior
        $userStartTime = Get-Date
        $endTime = $userStartTime.AddSeconds($DurationSeconds)
        
        $requestCount = 0
        $createdLinks = @()
        $results = @()
        
        while ((Get-Date) -lt $endTime -and $requestCount -lt $requestsPerUser) {
            # Create a new link
            $randomUrl = $testUrls | Get-Random
            $createResult = Test-CreateLink -url $randomUrl -userId $userId
            
            if ($createResult.Result) {
                $results += $createResult.Result
                
                if ($createResult.ShortCode) {
                    $createdLinks += $createResult.ShortCode
                    
                    # Sometimes access the link immediately
                    if ((Get-Random -Maximum 100) -lt 30) {
                        $accessResult = Test-AccessLink -shortCode $createResult.ShortCode -userId $userId
                        if ($accessResult) {
                            $results += $accessResult
                        }
                    }
                }
            }
            
            # Sometimes access a previously created link
            if ($createdLinks.Count -gt 0 -and (Get-Random -Maximum 100) -lt 50) {
                $randomLink = $createdLinks | Get-Random
                $accessResult = Test-AccessLink -shortCode $randomLink -userId $userId
                if ($accessResult) {
                    $results += $accessResult
                }
            }
            
            $requestCount++
            
            # Small delay between requests
            Start-Sleep -Milliseconds (Get-Random -Minimum 100 -Maximum 500)
        }
        
        return $results
    } -ArgumentList $i, $RequestsPerUser, $BaseUrl, $DurationSeconds, $testUrls
    $jobs += $job
}

# Wait for completion or timeout
$timeout = (Get-Date).AddSeconds($DurationSeconds + 30)
while ((Get-Job -State Running).Count -gt 0 -and (Get-Date) -lt $timeout) {
    Start-Sleep -Seconds 1
    $runningJobs = (Get-Job -State Running).Count
    Write-Host "$runningJobs users still running..." -ForegroundColor Yellow
}

# Collect results from jobs
$allJobResults = @()
foreach ($job in $jobs) {
    $jobResult = Receive-Job -Job $job
    if ($jobResult) {
        $allJobResults += $jobResult
    }
}

# Add results to global collection and collect errors
foreach ($result in $allJobResults) {
    $global:results.Add($result)
    if (-not $result.Success -and $result.Error) {
        $global:errors.Add("User $($result.UserId) - $($result.Operation): $($result.Error)")
    }
}

# Clean up jobs
$jobs | Stop-Job
$jobs | Remove-Job

Write-Host ""
Write-Host "Generating Results..." -ForegroundColor Green

# Calculate statistics
$allResults = $global:results.ToArray()
$totalRequests = $allResults.Count
$successfulRequests = ($allResults | Where-Object { $_.Success }).Count
$failedRequests = $totalRequests - $successfulRequests
$successRate = if ($totalRequests -gt 0) { ($successfulRequests / $totalRequests) * 100 } else { 0 }

$createLinkResults = $allResults | Where-Object { $_.Operation -eq 'CreateLink' -and $_.Success }
$accessLinkResults = $allResults | Where-Object { $_.Operation -eq 'AccessLink' -and $_.Success }

$avgCreateTime = if ($createLinkResults.Count -gt 0) { ($createLinkResults | Measure-Object -Property Duration -Average).Average } else { 0 }
$avgAccessTime = if ($accessLinkResults.Count -gt 0) { ($accessLinkResults | Measure-Object -Property Duration -Average).Average } else { 0 }

$maxCreateTime = if ($createLinkResults.Count -gt 0) { ($createLinkResults | Measure-Object -Property Duration -Maximum).Maximum } else { 0 }
$maxAccessTime = if ($accessLinkResults.Count -gt 0) { ($accessLinkResults | Measure-Object -Property Duration -Maximum).Maximum } else { 0 }

$testDuration = ((Get-Date) - $global:startTime).TotalSeconds
$requestsPerSecond = if ($testDuration -gt 0) { $totalRequests / $testDuration } else { 0 }

# Display results
Write-Host ""
Write-Host "=======================================" -ForegroundColor White
Write-Host "           LOAD TEST RESULTS           " -ForegroundColor White
Write-Host "=======================================" -ForegroundColor White
Write-Host ""
Write-Host "Overall Statistics:" -ForegroundColor Cyan
Write-Host "  Total Requests: $totalRequests"
Write-Host "  Successful: $successfulRequests"
Write-Host "  Failed: $failedRequests"
Write-Host "  Success Rate: $($successRate.ToString('F2'))%"
Write-Host "  Test Duration: $($testDuration.ToString('F2')) seconds"
Write-Host "  Requests/Second: $($requestsPerSecond.ToString('F2'))"
Write-Host ""

Write-Host "Performance Metrics:" -ForegroundColor Cyan
Write-Host "  Create Link - Avg: $($avgCreateTime.ToString('F2'))ms, Max: $($maxCreateTime.ToString('F2'))ms"
Write-Host "  Access Link - Avg: $($avgAccessTime.ToString('F2'))ms, Max: $($maxAccessTime.ToString('F2'))ms"
Write-Host ""

if ($global:errors.Count -gt 0) {
    Write-Host "Errors ($($global:errors.Count)):
" -ForegroundColor Red
    $global:errors.ToArray() | Select-Object -First 10 | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Red
    }
    if ($global:errors.Count -gt 10) {
        Write-Host "  ... and $($global:errors.Count - 10) more errors" -ForegroundColor Red
    }
    Write-Host ""
}

# Performance assessment
Write-Host "Performance Assessment:" -ForegroundColor Cyan
if ($successRate -ge 95) {
    Write-Host "  Excellent success rate!" -ForegroundColor Green
} elseif ($successRate -ge 90) {
    Write-Host "  Good success rate" -ForegroundColor Yellow
} else {
    Write-Host "  Poor success rate - needs investigation" -ForegroundColor Red
}

if ($avgCreateTime -le 1000) {
    Write-Host "  Good create link performance" -ForegroundColor Green
} elseif ($avgCreateTime -le 3000) {
    Write-Host "  Acceptable create link performance" -ForegroundColor Yellow
} else {
    Write-Host "  Slow create link performance" -ForegroundColor Red
}

if ($avgAccessTime -le 500) {
    Write-Host "  Excellent access link performance" -ForegroundColor Green
} elseif ($avgAccessTime -le 1000) {
    Write-Host "  Acceptable access link performance" -ForegroundColor Yellow
} else {
    Write-Host "  Slow access link performance" -ForegroundColor Red
}

Write-Host ""
Write-Host "Load test completed!" -ForegroundColor Green
