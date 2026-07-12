[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Low")]
param(
    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [switch]$CaptureLive,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [switch]$Finalize,

    [Parameter(Mandatory, ParameterSetName = "ValidateOnly")]
    [switch]$ValidateOnly,

    [Parameter(Mandatory, ParameterSetName = "ValidateOnly")]
    [string]$EvidencePath,

    [Parameter(ParameterSetName = "CaptureLive")]
    [Parameter(ParameterSetName = "Finalize")]
    [string]$OutputPath,

    [Parameter(ParameterSetName = "CaptureLive")]
    [Parameter(ParameterSetName = "Finalize")]
    [Parameter(ParameterSetName = "ValidateOnly")]
    [string]$SchemaPath = (Join-Path $PSScriptRoot "phase-2-evidence.schema.json"),

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [DateTimeOffset]$HpaBaselineObservedAtUtc,

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [DateTimeOffset]$HpaPeakObservedAtUtc,

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [DateTimeOffset]$HpaRecoveredAtUtc,

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [ValidateRange(3, 6)]
    [int]$HpaPeakReplicas,

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [ValidateRange(0.0, 1.0)]
    [double]$HttpReqFailedRate,

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [ValidateRange(0.0, 60000.0)]
    [double]$HttpReqDurationP95Ms,

    [Parameter(Mandatory, ParameterSetName = "CaptureLive")]
    [DateTimeOffset]$SnsNotificationObservedAtUtc,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [string]$LiveEvidencePath,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [string]$GroupIdentifier,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [System.Collections.IDictionary[]]$Participants,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [string]$VideoUrl,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [ValidateSet("public", "unlisted")]
    [string]$VideoVisibility,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [ValidateRange(1, 900)]
    [int]$VideoDurationSeconds,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [switch]$AnonymousPlaybackVerified,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [DateTimeOffset]$VideoVerifiedAtUtc,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [string]$StateBucketName,

    [Parameter(Mandatory, ParameterSetName = "Finalize")]
    [switch]$DestroyVerificationPassed,

    [Parameter(ParameterSetName = "CaptureLive")]
    [Parameter(ParameterSetName = "Finalize")]
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$CanonicalDeliveryRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$MaximumProjectionCharacters = 1MB
$ForbiddenPropertyPattern = '(?i)(credential|password|passwd|pwd|token|secret|connectionstring|apikey|accesskey|privatekey|sessiontoken|hmackey|jwtkey|rawlog|state|plan|terraformstate|terraformplan)'
$ForbiddenValuePatterns = @(
    '(?i)-----BEGIN [A-Z ]*PRIVATE KEY-----',
    '\b(?:AKIA|ASIA)[A-Z0-9]{16}\b',
    '\bgh[pousr]_[A-Za-z0-9]{20,}\b',
    '(?i)\beyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\b',
    '(?i)\bBearer\s+[A-Za-z0-9._~+/\-]+=*',
    '(?i)(?:^|[;\s])(password|passwd|pwd|token|secret|credential|access[_-]?key|session[_-]?token)\s*[:=]',
    '(?i)(?:Host|Server)=[^;]+;[^\r\n]*(?:Password|Pwd)=',
    '(?i)\b(?:postgres|postgresql|mysql|sqlserver)://[^\s/@:]+:[^\s/@]+@',
    '(?i)"terraform_version"\s*:',
    '(?i)Saved the plan to:',
    '(?i)\b(raw[-_ ]?(?:log|state|plan)|terraform[-_ ]?(?:state|plan))\b',
    '(?i)\b(TBD|TODO|CHANGEME|PLACEHOLDER)\b'
)

function Assert-Condition {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Assert-NoReparsePointChain {
    param([string]$Path)

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $pathRoot = [System.IO.Path]::GetPathRoot($fullPath)
    $relative = $fullPath.Substring($pathRoot.Length)
    $current = $pathRoot
    foreach ($segment in $relative -split '[\\/]') {
        if ([string]::IsNullOrWhiteSpace($segment)) { continue }
        $current = Join-Path $current $segment
        if (Test-Path -LiteralPath $current) {
            $item = Get-Item -LiteralPath $current -Force
            Assert-Condition (-not ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) `
                "Canonical evidence path must not traverse a symlink or reparse point."
        }
    }
}

function Assert-CanonicalDeliveryPath {
    param(
        [AllowEmptyString()][string]$Candidate,
        [string]$ExpectedFileName,
        [switch]$MustExist
    )

    Assert-Condition ($ExpectedFileName -in @(
        "phase-2-evidence.schema.json",
        "phase-2-live-evidence.json",
        "phase-2-evidence.json")) "Unexpected canonical evidence filename."
    $expectedPath = [System.IO.Path]::GetFullPath((Join-Path $CanonicalDeliveryRoot $ExpectedFileName))
    $candidatePath = if ([string]::IsNullOrWhiteSpace($Candidate)) {
        $expectedPath
    }
    else {
        [System.IO.Path]::GetFullPath($Candidate)
    }
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    Assert-Condition ($candidatePath.Equals($expectedPath, $comparison)) `
        "Evidence paths are restricted to the canonical docs/delivery filename for this mode."
    Assert-NoReparsePointChain -Path $CanonicalDeliveryRoot
    Assert-NoReparsePointChain -Path $candidatePath
    if ($MustExist) {
        Assert-Condition (Test-Path -LiteralPath $candidatePath -PathType Leaf) "Canonical evidence input does not exist."
    }
    return $expectedPath
}

function Assert-JsonBooleanTrue {
    param([object]$Value, [string]$Location)

    Assert-Condition ($Value -is [bool]) "$Location must be a JSON boolean."
    Assert-Condition ($Value -eq $true) "$Location must be true."
    return $true
}

function ConvertTo-UtcText {
    param([DateTimeOffset]$Value)
    return $Value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", [Globalization.CultureInfo]::InvariantCulture)
}

function ConvertFrom-StrictUtcText {
    param([string]$Value, [string]$Location)

    Assert-Condition ($Value -match '^[0-9]{4}-(0[1-9]|1[0-2])-([0-2][0-9]|3[01])T([01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9](\.[0-9]{1,7})?Z$') `
        "$Location must be an uppercase-Z UTC timestamp."
    $parsed = [DateTimeOffset]::MinValue
    Assert-Condition ([DateTimeOffset]::TryParse(
        $Value,
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::AssumeUniversal,
        [ref]$parsed)) "$Location is not a valid timestamp."
    Assert-Condition ($parsed.Offset -eq [TimeSpan]::Zero) "$Location must be UTC."
    return $parsed
}

function Assert-NoSensitiveEvidence {
    param([object]$Value, [string]$Location = "evidence")

    if ($null -eq $Value) { return }
    if ($Value -is [string]) {
        Assert-Condition (-not $Value.Contains("`r") -and -not $Value.Contains("`n")) `
            "$Location must not contain multiline or raw command output."
        foreach ($pattern in $ForbiddenValuePatterns) {
            Assert-Condition ($Value -notmatch $pattern) "$Location contains a forbidden sensitive/raw value pattern."
        }
        return
    }
    if ($Value -is [System.Collections.IDictionary]) {
        foreach ($key in $Value.Keys) {
            Assert-Condition ([string]$key -notmatch $ForbiddenPropertyPattern) `
                "$Location contains forbidden property name '$key'."
            Assert-NoSensitiveEvidence -Value $Value[$key] -Location "$Location.$key"
        }
        return
    }
    if ($Value -is [pscustomobject]) {
        foreach ($property in $Value.PSObject.Properties) {
            Assert-Condition ($property.Name -notmatch $ForbiddenPropertyPattern) `
                "$Location contains forbidden property name '$($property.Name)'."
            Assert-NoSensitiveEvidence -Value $property.Value -Location "$Location.$($property.Name)"
        }
        return
    }
    if ($Value -is [System.Collections.IEnumerable]) {
        $items = @($Value)
        Assert-Condition ($items.Count -gt 0) "$Location must not be an empty array."
        for ($index = 0; $index -lt $items.Count; $index++) {
            Assert-NoSensitiveEvidence -Value $items[$index] -Location "$Location[$index]"
        }
    }
}

function Assert-RealUrl {
    param([string]$Value, [bool]$RequireHttps, [string]$Location)

    $uri = $null
    Assert-Condition ([Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)) "$Location must be an absolute URL."
    $allowedSchemes = if ($RequireHttps) { @("https") } else { @("http", "https") }
    Assert-Condition ($allowedSchemes -contains $uri.Scheme.ToLowerInvariant()) "$Location uses a forbidden URL scheme."
    Assert-Condition ([string]::IsNullOrEmpty($uri.UserInfo)) "$Location must not contain URL user information."
    $urlHost = $uri.DnsSafeHost.ToLowerInvariant().TrimEnd('.')
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($urlHost)) "$Location must include a host."
    Assert-Condition ($urlHost -notin @("localhost", "example.com", "example.org", "example.net")) "$Location uses a forbidden host."
    Assert-Condition ($urlHost -notmatch '(^|\.)(local|test|invalid|example)$') "$Location uses a reserved test/local host."

    $address = $null
    if ([Net.IPAddress]::TryParse($urlHost, [ref]$address)) {
        Assert-Condition (-not [Net.IPAddress]::IsLoopback($address)) "$Location must not use loopback."
        if ($address.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork) {
            $bytes = $address.GetAddressBytes()
            $private = $bytes[0] -eq 10 -or
                ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or
                ($bytes[0] -eq 192 -and $bytes[1] -eq 168) -or
                ($bytes[0] -eq 169 -and $bytes[1] -eq 254) -or
                $bytes[0] -eq 0
            Assert-Condition (-not $private) "$Location must not use a private/link-local address."
        }
        elseif ($address.IsIPv6LinkLocal -or $address.IsIPv6SiteLocal -or (($address.GetAddressBytes()[0] -band 0xFE) -eq 0xFC)) {
            throw "$Location must not use a private/link-local IPv6 address."
        }
    }
    return $uri
}

function Assert-WorkflowRun {
    param([object]$Run, [string]$ExpectedName, [object]$Repository, [string]$Location)

    Assert-Condition ([string]$Run.workflowName -ceq $ExpectedName) "$Location workflow name is wrong."
    Assert-Condition ([string]$Run.status -ceq "completed" -and [string]$Run.conclusion -ceq "success") `
        "$Location must be completed successfully."
    $started = ConvertFrom-StrictUtcText -Value ([string]$Run.startedAtUtc) -Location "$Location.startedAtUtc"
    $completed = ConvertFrom-StrictUtcText -Value ([string]$Run.completedAtUtc) -Location "$Location.completedAtUtc"
    Assert-Condition ($started -lt $completed) "$Location timestamps are not ordered."
    $null = Assert-RealUrl -Value ([string]$Run.url) -RequireHttps $true -Location "$Location.url"
    $expectedPrefix = ([string]$Repository.url).TrimEnd('/') + "/actions/runs/"
    Assert-Condition ([string]$Run.url -like "$expectedPrefix*") "$Location URL does not belong to the evidence repository."
}

function Assert-LiveEvidenceSemantics {
    param([object]$Evidence, [string]$Location = "live evidence")

    Assert-Condition ([string]$Evidence.evidenceType -ceq "live") "$Location has the wrong evidence type."
    $null = Assert-RealUrl -Value ([string]$Evidence.repository.url) -RequireHttps $true -Location "$Location.repository.url"
    $null = Assert-RealUrl -Value ([string]$Evidence.api.baseUrl) -RequireHttps $false -Location "$Location.api.baseUrl"
    Assert-WorkflowRun -Run $Evidence.deployRun -ExpectedName "Deploy AWS Academy" -Repository $Evidence.repository -Location "$Location.deployRun"
    Assert-Condition ([string]$Evidence.repository.commitSha -ceq [string]$Evidence.deployRun.headSha) `
        "$Location deploy SHA differs from repository SHA."
    Assert-Condition ([string]$Evidence.repository.commitSha -ceq [string]$Evidence.aws.ecr.imageTag) `
        "$Location ECR tag differs from repository SHA."
    foreach ($pod in @($Evidence.kubernetes.pods)) {
        Assert-Condition ([string]$pod.imageDigest -ceq [string]$Evidence.aws.ecr.imageDigest) `
            "$Location pod digest differs from ECR digest."
    }
    $podNames = @($Evidence.kubernetes.pods | ForEach-Object { [string]$_.name })
    Assert-Condition (@($podNames | Sort-Object -Unique).Count -eq $podNames.Count) "$Location contains duplicate pod names."
    Assert-Condition ([int]$Evidence.kubernetes.deployment.desiredReplicas -eq 2 -and
                      [int]$Evidence.kubernetes.deployment.readyReplicas -eq 2 -and
                      [int]$Evidence.kubernetes.deployment.availableReplicas -eq 2 -and
                      @($Evidence.kubernetes.pods).Count -eq 2) `
        "$Location must prove recovery to exactly two ready/available pods."

    $deployCompleted = ConvertFrom-StrictUtcText ([string]$Evidence.deployRun.completedAtUtc) "$Location.deployRun.completedAtUtc"
    $baseline = ConvertFrom-StrictUtcText ([string]$Evidence.hpaDemonstration.baseline.observedAtUtc) "$Location.hpaDemonstration.baseline.observedAtUtc"
    $peak = ConvertFrom-StrictUtcText ([string]$Evidence.hpaDemonstration.peak.observedAtUtc) "$Location.hpaDemonstration.peak.observedAtUtc"
    $recovery = ConvertFrom-StrictUtcText ([string]$Evidence.hpaDemonstration.recovery.observedAtUtc) "$Location.hpaDemonstration.recovery.observedAtUtc"
    $notification = ConvertFrom-StrictUtcText ([string]$Evidence.aws.sns.notificationObservedAtUtc) "$Location.aws.sns.notificationObservedAtUtc"
    $captured = ConvertFrom-StrictUtcText ([string]$Evidence.capturedAtUtc) "$Location.capturedAtUtc"
    Assert-Condition ($deployCompleted -lt $baseline -and $baseline -lt $peak -and $peak -lt $recovery -and $recovery -le $captured) `
        "$Location deploy/HPA/capture timestamps are not strictly ordered."
    Assert-Condition ($deployCompleted -lt $notification -and $notification -le $captured) `
        "$Location SNS observation must occur after deploy and before capture."
    Assert-Condition ([int]$Evidence.hpaDemonstration.peak.maximumReplicasObserved -le [int]$Evidence.kubernetes.hpa.maximumReplicas) `
        "$Location HPA peak exceeds the configured maximum."
}

function Assert-EvidenceSemantics {
    param([object]$Evidence)

    if ([string]$Evidence.evidenceType -ceq "live") {
        Assert-LiveEvidenceSemantics -Evidence $Evidence
        return
    }
    Assert-Condition ([string]$Evidence.evidenceType -ceq "final") "Unknown evidence type."
    Assert-LiveEvidenceSemantics -Evidence $Evidence.liveEvidence -Location "final evidence.liveEvidence"
    Assert-WorkflowRun -Run $Evidence.qualityGateRun -ExpectedName "Quality Gate" -Repository $Evidence.liveEvidence.repository -Location "final evidence.qualityGateRun"
    Assert-WorkflowRun -Run $Evidence.destroy.run -ExpectedName "Destroy AWS Academy" -Repository $Evidence.liveEvidence.repository -Location "final evidence.destroy.run"
    Assert-Condition ([string]$Evidence.qualityGateRun.headSha -ceq [string]$Evidence.liveEvidence.repository.commitSha) `
        "Quality Gate SHA differs from the demonstrated commit."
    Assert-Condition ([string]$Evidence.destroy.run.headSha -ceq [string]$Evidence.liveEvidence.repository.commitSha) `
        "Destroy SHA differs from the demonstrated commit."
    $null = Assert-RealUrl -Value ([string]$Evidence.video.url) -RequireHttps $true -Location "final evidence.video.url"

    $participantIds = @($Evidence.participants | ForEach-Object { [string]$_.identifier })
    Assert-Condition (@($participantIds | Sort-Object -Unique).Count -eq $participantIds.Count) `
        "Participant identifiers must be unique."

    $captured = ConvertFrom-StrictUtcText ([string]$Evidence.liveEvidence.capturedAtUtc) "final evidence.liveEvidence.capturedAtUtc"
    $destroyStarted = ConvertFrom-StrictUtcText ([string]$Evidence.destroy.run.startedAtUtc) "final evidence.destroy.run.startedAtUtc"
    $destroyCompleted = ConvertFrom-StrictUtcText ([string]$Evidence.destroy.run.completedAtUtc) "final evidence.destroy.run.completedAtUtc"
    $destroyVerified = ConvertFrom-StrictUtcText ([string]$Evidence.destroy.verifiedAtUtc) "final evidence.destroy.verifiedAtUtc"
    $bucketVerified = ConvertFrom-StrictUtcText ([string]$Evidence.retainedBackendBucket.verifiedAtUtc) "final evidence.retainedBackendBucket.verifiedAtUtc"
    $videoVerified = ConvertFrom-StrictUtcText ([string]$Evidence.video.verifiedAtUtc) "final evidence.video.verifiedAtUtc"
    $finalized = ConvertFrom-StrictUtcText ([string]$Evidence.finalizedAtUtc) "final evidence.finalizedAtUtc"
    Assert-Condition ($captured -lt $destroyStarted -and $destroyStarted -lt $destroyCompleted) `
        "Live capture must precede the completed destroy."
    Assert-Condition ($destroyCompleted -le $destroyVerified -and $destroyCompleted -le $bucketVerified -and $destroyCompleted -le $videoVerified) `
        "Destroy, bucket, and anonymous video verification timestamps are inconsistent."
    Assert-Condition ($destroyVerified -le $finalized -and $bucketVerified -le $finalized -and $videoVerified -le $finalized) `
        "Finalized timestamp must follow every final verification."
}

function Assert-EvidenceValid {
    param([object]$Evidence, [string]$EffectiveSchemaPath)

    Assert-NoSensitiveEvidence -Value $Evidence
    $json = $Evidence | ConvertTo-Json -Depth 100
    $schemaValid = $json | Test-Json -SchemaFile $EffectiveSchemaPath -ErrorAction SilentlyContinue
    Assert-Condition $schemaValid "Evidence does not satisfy phase-2-evidence.schema.json."
    Assert-EvidenceSemantics -Evidence $Evidence
    return $json
}

function Read-Evidence {
    param([string]$Path, [string]$EffectiveSchemaPath, [string]$ExpectedType)

    $resolved = Resolve-Path -LiteralPath $Path -ErrorAction Stop
    $file = Get-Item -LiteralPath $resolved
    Assert-Condition ($file.Length -gt 0 -and $file.Length -le 1MB) "Evidence input size is invalid."
    $evidence = Get-Content -LiteralPath $resolved -Raw | ConvertFrom-Json -Depth 100 -DateKind String
    $null = Assert-EvidenceValid -Evidence $evidence -EffectiveSchemaPath $EffectiveSchemaPath
    if (-not [string]::IsNullOrWhiteSpace($ExpectedType)) {
        Assert-Condition ([string]$evidence.evidenceType -ceq $ExpectedType) "Evidence input has the wrong type."
    }
    return $evidence
}

function Assert-OutputAvailable {
    param([string]$Path, [switch]$AllowOverwrite)
    if (Test-Path -LiteralPath $Path) {
        Assert-Condition $AllowOverwrite "Output already exists; use -Force only after verifying the target path."
    }
}

function Write-AtomicEvidence {
    param([object]$Evidence, [string]$Path, [string]$EffectiveSchemaPath, [switch]$AllowOverwrite)

    $json = Assert-EvidenceValid -Evidence $Evidence -EffectiveSchemaPath $EffectiveSchemaPath
    Assert-OutputAvailable -Path $Path -AllowOverwrite:$AllowOverwrite
    $absolutePath = [System.IO.Path]::GetFullPath($Path)
    $directory = [System.IO.Path]::GetDirectoryName($absolutePath)
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $temporaryPath = Join-Path $directory ("." + [System.IO.Path]::GetFileName($absolutePath) + "." + [Guid]::NewGuid().ToString("N") + ".tmp")
    try {
        $encoding = [System.Text.UTF8Encoding]::new($false)
        $bytes = $encoding.GetBytes($json + "`n")
        $stream = [System.IO.FileStream]::new(
            $temporaryPath,
            [System.IO.FileMode]::CreateNew,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        try {
            $stream.Write($bytes, 0, $bytes.Length)
            $stream.Flush($true)
        }
        finally {
            $stream.Dispose()
        }
        $null = Read-Evidence -Path $temporaryPath -EffectiveSchemaPath $EffectiveSchemaPath -ExpectedType ([string]$Evidence.evidenceType)
        [System.IO.File]::Move($temporaryPath, $absolutePath, [bool]$AllowOverwrite)
    }
    finally {
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-ProjectionSpec {
    param(
        [ValidateSet(
            "GitRoot", "GitHead", "GitStatus", "GhRepository", "GhDeployRun", "GhQualityRun", "GhDestroyRun",
            "AwsIdentity", "AwsEksCluster", "AwsEksNodeGroup", "AwsRds", "AwsEcrImage", "AwsSnsTopic",
            "AwsSnsSubscriptions", "KubectlService", "KubectlDeployment", "KubectlPods", "KubectlHpa",
            "AwsBucketHead", "AwsBucketVersioning", "AwsBucketPublicAccess", "AwsBucketEncryption")]
        [string]$Operation,
        [hashtable]$Context = @{}
    )

    switch ($Operation) {
        "GitRoot" { return @{ Command = "git"; Arguments = @("rev-parse", "--show-toplevel") } }
        "GitHead" { return @{ Command = "git"; Arguments = @("rev-parse", "HEAD") } }
        "GitStatus" { return @{ Command = "git"; Arguments = @("status", "--porcelain", "--untracked-files=all"); AllowEmpty = $true } }
        "GhRepository" { return @{ Command = "gh"; Arguments = @("repo", "view", "--json", "nameWithOwner,url", "--jq", "{nameWithOwner:.nameWithOwner,url:.url}") } }
        "GhDeployRun" { return @{ Command = "gh"; Arguments = @("run", "list", "--workflow", "deploy-aws-academy.yml", "--commit", $Context.Sha, "--status", "success", "--limit", "1", "--json", "databaseId,url,status,conclusion,startedAt,updatedAt,headSha") } }
        "GhQualityRun" { return @{ Command = "gh"; Arguments = @("run", "list", "--workflow", "quality-gate.yml", "--commit", $Context.Sha, "--status", "success", "--limit", "1", "--json", "databaseId,url,status,conclusion,startedAt,updatedAt,headSha") } }
        "GhDestroyRun" { return @{ Command = "gh"; Arguments = @("run", "list", "--workflow", "destroy-aws-academy.yml", "--commit", $Context.Sha, "--status", "success", "--limit", "1", "--json", "databaseId,url,status,conclusion,startedAt,updatedAt,headSha") } }
        "AwsIdentity" { return @{ Command = "aws"; Arguments = @("sts", "get-caller-identity", "--query", "{account:Account,arn:Arn}", "--output", "json") } }
        "AwsEksCluster" { return @{ Command = "aws"; Arguments = @("eks", "describe-cluster", "--region", "us-east-1", "--name", "garageflow-academy", "--query", "cluster.{clusterName:name,status:status,version:version}", "--output", "json") } }
        "AwsEksNodeGroup" { return @{ Command = "aws"; Arguments = @("eks", "describe-nodegroup", "--region", "us-east-1", "--cluster-name", "garageflow-academy", "--nodegroup-name", "garageflow-academy-workers", "--query", "nodegroup.{nodeGroupName:nodegroupName,nodeGroupStatus:status}", "--output", "json") } }
        "AwsRds" { return @{ Command = "aws"; Arguments = @("rds", "describe-db-instances", "--region", "us-east-1", "--db-instance-identifier", "garageflow-academy", "--query", "DBInstances[0].{identifier:DBInstanceIdentifier,status:DBInstanceStatus,engine:Engine,publiclyAccessible:PubliclyAccessible}", "--output", "json") } }
        "AwsEcrImage" { return @{ Command = "aws"; Arguments = @("ecr", "describe-images", "--region", "us-east-1", "--repository-name", "garageflow", "--image-ids", "imageTag=$($Context.Sha)", "--query", "imageDetails[0].{imageDigest:imageDigest,imageTags:imageTags}", "--output", "json") } }
        "AwsSnsTopic" { return @{ Command = "aws"; Arguments = @("sns", "list-topics", "--region", "us-east-1", "--query", "Topics[?ends_with(TopicArn, ':garageflow-work-orders')].TopicArn | [0]", "--output", "text") } }
        "AwsSnsSubscriptions" { return @{ Command = "aws"; Arguments = @("sns", "list-subscriptions-by-topic", "--region", "us-east-1", "--topic-arn", $Context.TopicArn, "--query", 'Subscriptions[?Protocol==`email` && SubscriptionArn!=`PendingConfirmation`].{protocol:Protocol,subscriptionArn:SubscriptionArn}', "--output", "json") } }
        "KubectlService" { return @{ Command = "kubectl"; Arguments = @("-n", "garageflow", "get", "service", "garageflow-api", "-o", 'jsonpath={.spec.type}{"|"}{.status.loadBalancer.ingress[0].hostname}{"|"}{.status.loadBalancer.ingress[0].ip}') } }
        "KubectlDeployment" { return @{ Command = "kubectl"; Arguments = @("-n", "garageflow", "get", "deployment", "garageflow-api", "-o", 'jsonpath={.metadata.name}{"|"}{.spec.replicas}{"|"}{.status.readyReplicas}{"|"}{.status.availableReplicas}') } }
        "KubectlPods" { return @{ Command = "kubectl"; Arguments = @("-n", "garageflow", "get", "pods", "-l", "app.kubernetes.io/name=garageflow-api", "-o", 'jsonpath={range .items[*]}{.metadata.name}{"|"}{.status.phase}{"|"}{.status.containerStatuses[0].ready}{"|"}{.status.containerStatuses[0].restartCount}{"|"}{.status.containerStatuses[0].imageID}{"\n"}{end}') } }
        "KubectlHpa" { return @{ Command = "kubectl"; Arguments = @("-n", "garageflow", "get", "hpa", "garageflow-api", "-o", 'jsonpath={.metadata.name}{"|"}{.spec.minReplicas}{"|"}{.spec.maxReplicas}{"|"}{.status.currentReplicas}{"|"}{.status.desiredReplicas}{"|"}{.spec.metrics[0].resource.target.averageUtilization}{"|"}{.spec.metrics[1].resource.target.averageUtilization}') } }
        "AwsBucketHead" { return @{ Command = "aws"; Arguments = @("s3api", "head-bucket", "--bucket", $Context.BucketName); AllowEmpty = $true } }
        "AwsBucketVersioning" { return @{ Command = "aws"; Arguments = @("s3api", "get-bucket-versioning", "--bucket", $Context.BucketName, "--query", "{status:Status}", "--output", "json") } }
        "AwsBucketPublicAccess" { return @{ Command = "aws"; Arguments = @("s3api", "get-public-access-block", "--bucket", $Context.BucketName, "--query", "PublicAccessBlockConfiguration.{blockPublicAcls:BlockPublicAcls,ignorePublicAcls:IgnorePublicAcls,blockPublicPolicy:BlockPublicPolicy,restrictPublicBuckets:RestrictPublicBuckets}", "--output", "json") } }
        "AwsBucketEncryption" { return @{ Command = "aws"; Arguments = @("s3api", "get-bucket-encryption", "--bucket", $Context.BucketName, "--query", "ServerSideEncryptionConfiguration.Rules[0].ApplyServerSideEncryptionByDefault.{algorithm:SSEAlgorithm}", "--output", "json") } }
    }
}

function Invoke-AllowlistedProjection {
    param([string]$Operation, [hashtable]$Context = @{})

    $spec = Get-ProjectionSpec -Operation $Operation -Context $Context
    $commandInfo = Get-Command $spec.Command -ErrorAction SilentlyContinue
    Assert-Condition ($null -ne $commandInfo) "Required tool for projection '$Operation' is unavailable."
    $projectionArguments = @($spec.Arguments)
    $output = @(& $spec.Command @projectionArguments 2>$null)
    $succeeded = $?
    if ($commandInfo.CommandType -in @("Application", "ExternalScript")) {
        $succeeded = $succeeded -and $LASTEXITCODE -eq 0
    }
    Assert-Condition $succeeded "Allowlisted projection '$Operation' failed."
    $text = ($output | ForEach-Object { [string]$_ }) -join "`n"
    Assert-Condition ($text.Length -le $MaximumProjectionCharacters) "Allowlisted projection '$Operation' exceeded the output cap."
    $allowEmpty = $spec.ContainsKey("AllowEmpty") -and [bool]$spec["AllowEmpty"]
    if (-not $allowEmpty) {
        Assert-Condition (-not [string]::IsNullOrWhiteSpace($text)) "Allowlisted projection '$Operation' returned no data."
    }
    return $text.Trim()
}

function ConvertFrom-ProjectionJson {
    param([string]$Text, [string[]]$ExpectedProperties, [string]$Location)

    try {
        $value = $Text | ConvertFrom-Json -Depth 30 -DateKind String -ErrorAction Stop
    }
    catch {
        throw "$Location did not return valid projected JSON."
    }
    Assert-Condition ($null -ne $value) "$Location returned null."
    $actual = @($value.PSObject.Properties.Name | Sort-Object)
    $expected = @($ExpectedProperties | Sort-Object)
    Assert-Condition (($actual -join "|") -ceq ($expected -join "|")) "$Location returned unexpected projected properties."
    Assert-NoSensitiveEvidence -Value $value -Location $Location
    return $value
}

function ConvertFrom-WorkflowProjection {
    param([string]$Text, [string]$WorkflowName, [string]$Location)

    try {
        $runs = @($Text | ConvertFrom-Json -Depth 20 -DateKind String -ErrorAction Stop)
    }
    catch {
        throw "$Location did not return valid workflow JSON."
    }
    Assert-Condition ($runs.Count -eq 1 -and $null -ne $runs[0]) "$Location must return exactly one successful run."
    $run = $runs[0]
    $actual = @($run.PSObject.Properties.Name | Sort-Object)
    $expected = @("databaseId", "url", "status", "conclusion", "startedAt", "updatedAt", "headSha" | Sort-Object)
    Assert-Condition (($actual -join "|") -ceq ($expected -join "|")) "$Location returned unexpected workflow properties."
    return [ordered]@{
        workflowName = $WorkflowName
        runId = [long]$run.databaseId
        url = [string]$run.url
        headSha = [string]$run.headSha
        status = [string]$run.status
        conclusion = [string]$run.conclusion
        startedAtUtc = ConvertTo-UtcText ([DateTimeOffset]::Parse([string]$run.startedAt, [Globalization.CultureInfo]::InvariantCulture))
        completedAtUtc = ConvertTo-UtcText ([DateTimeOffset]::Parse([string]$run.updatedAt, [Globalization.CultureInfo]::InvariantCulture))
    }
}

function Assert-RepositoryState {
    param([string[]]$AllowedRelativePaths)

    $reportedRoot = [System.IO.Path]::GetFullPath((Invoke-AllowlistedProjection "GitRoot"))
    Assert-Condition ($reportedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) -ceq $RepositoryRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar)) `
        "Collector must run inside the expected repository worktree."
    $status = Invoke-AllowlistedProjection "GitStatus"
    if ([string]::IsNullOrWhiteSpace($status)) { return }
    $allowed = @($AllowedRelativePaths | ForEach-Object { $_.Replace("\", "/").TrimStart("./") })
    foreach ($line in $status -split "`r?`n") {
        Assert-Condition ($line.Length -ge 4) "Git status projection was malformed."
        $path = $line.Substring(3).Trim().Replace("\", "/")
        if ($path.Contains(" -> ")) { $path = ($path -split " -> ")[-1] }
        Assert-Condition ($allowed -contains $path) "Repository contains unrelated changes; evidence capture/finalization stopped."
    }
}

function Get-RelativeRepositoryPath {
    param([string]$Path)
    $absolute = [System.IO.Path]::GetFullPath($Path)
    return [System.IO.Path]::GetRelativePath($RepositoryRoot, $absolute).Replace("\", "/")
}

function Split-ExactProjection {
    param([string]$Text, [int]$Count, [string]$Location)
    $parts = @($Text.Split([char]'|'))
    Assert-Condition ($parts.Count -eq $Count) "$Location projection shape is invalid."
    return $parts
}

function Get-RepositoryProjection {
    $repository = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "GhRepository") `
        -ExpectedProperties @("nameWithOwner", "url") `
        -Location "GitHub repository"
    $null = Assert-RealUrl -Value ([string]$repository.url) -RequireHttps $true -Location "GitHub repository URL"
    $expectedUrl = "https://github.com/$($repository.nameWithOwner)"
    Assert-Condition (([string]$repository.url).TrimEnd('/') -ceq $expectedUrl) "GitHub repository name and URL differ."
    return $repository
}

function Assert-AwsIdentityProjection {
    $identity = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsIdentity") `
        -ExpectedProperties @("account", "arn") `
        -Location "AWS identity"
    Assert-Condition ([string]$identity.account -match '^[0-9]{12}$') "AWS identity account is invalid."
    Assert-Condition ([string]$identity.arn -match '^arn:aws:sts::[0-9]{12}:assumed-role/[A-Za-z0-9+=,.@_/-]+$') `
        "AWS identity must be an assumed Academy role."
    return [string]$identity.account
}

function New-LiveEvidence {
    param([string]$EffectiveOutputPath)

    Assert-Condition ($HttpReqFailedRate -ge 0 -and $HttpReqFailedRate -lt 0.02) `
        "http_req_failed must be below 0.02."
    Assert-Condition ($HttpReqDurationP95Ms -ge 0 -and $HttpReqDurationP95Ms -lt 1000) `
        "http_req_duration p95 must be below 1000 ms."

    $allowedPaths = @(
        (Get-RelativeRepositoryPath $EffectiveOutputPath),
        "docs/delivery/phase-2-live-evidence.json",
        "docs/delivery/phase-2-evidence.json"
    ) | Sort-Object -Unique
    Assert-RepositoryState -AllowedRelativePaths $allowedPaths

    $sha = (Invoke-AllowlistedProjection "GitHead").Trim()
    Assert-Condition ($sha -match '^[0-9a-f]{40}$') "Git HEAD must be a lowercase 40-hex commit SHA."
    $repository = Get-RepositoryProjection
    $deployRun = ConvertFrom-WorkflowProjection `
        -Text (Invoke-AllowlistedProjection "GhDeployRun" @{ Sha = $sha }) `
        -WorkflowName "Deploy AWS Academy" `
        -Location "deploy workflow"

    $null = Assert-AwsIdentityProjection
    $eksCluster = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsEksCluster") `
        -ExpectedProperties @("clusterName", "status", "version") `
        -Location "EKS cluster"
    $eksNodeGroup = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsEksNodeGroup") `
        -ExpectedProperties @("nodeGroupName", "nodeGroupStatus") `
        -Location "EKS node group"
    $rds = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsRds") `
        -ExpectedProperties @("identifier", "status", "engine", "publiclyAccessible") `
        -Location "RDS instance"
    $ecrImage = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsEcrImage" @{ Sha = $sha }) `
        -ExpectedProperties @("imageDigest", "imageTags") `
        -Location "ECR image"
    Assert-Condition (@($ecrImage.imageTags).Count -gt 0 -and @($ecrImage.imageTags) -contains $sha) `
        "ECR projection does not contain the demonstrated SHA tag."

    $topicArn = Invoke-AllowlistedProjection "AwsSnsTopic"
    Assert-Condition ($topicArn -match '^arn:aws:sns:us-east-1:[0-9]{12}:garageflow-work-orders$') `
        "SNS topic projection is invalid."
    try {
        $subscriptions = @(Invoke-AllowlistedProjection "AwsSnsSubscriptions" @{ TopicArn = $topicArn } | ConvertFrom-Json -Depth 20 -DateKind String -ErrorAction Stop)
    }
    catch {
        throw "SNS subscription projection is invalid."
    }
    Assert-Condition ($subscriptions.Count -ge 1) "At least one confirmed SNS email subscription is required."
    foreach ($subscription in $subscriptions) {
        $actual = @($subscription.PSObject.Properties.Name | Sort-Object)
        Assert-Condition (($actual -join "|") -ceq ((@("protocol", "subscriptionArn") | Sort-Object) -join "|")) `
            "SNS subscription projection has unexpected properties."
        Assert-Condition ([string]$subscription.protocol -ceq "email" -and [string]$subscription.subscriptionArn -match '^arn:aws:sns:') `
            "SNS subscription must be a confirmed email subscription."
    }

    $serviceParts = Split-ExactProjection (Invoke-AllowlistedProjection "KubectlService") 3 "Service"
    Assert-Condition ($serviceParts[0] -ceq "LoadBalancer") "GarageFlow Service must be LoadBalancer."
    $ingress = if (-not [string]::IsNullOrWhiteSpace($serviceParts[1])) { $serviceParts[1] } else { $serviceParts[2] }
    Assert-Condition (-not [string]::IsNullOrWhiteSpace($ingress)) "GarageFlow Service has no public ingress."
    $apiUrl = "http://$ingress"

    $deploymentParts = Split-ExactProjection (Invoke-AllowlistedProjection "KubectlDeployment") 4 "Deployment"
    $deployment = [ordered]@{
        name = $deploymentParts[0]
        desiredReplicas = [int]$deploymentParts[1]
        readyReplicas = [int]$deploymentParts[2]
        availableReplicas = [int]$deploymentParts[3]
    }

    $podLines = @((Invoke-AllowlistedProjection "KubectlPods") -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $pods = @()
    foreach ($line in $podLines) {
        $parts = Split-ExactProjection $line 5 "Pod"
        $digestMatch = [regex]::Match($parts[4], 'sha256:[0-9a-f]{64}$')
        Assert-Condition $digestMatch.Success "Pod image projection does not contain an immutable digest."
        $pods += [ordered]@{
            name = $parts[0]
            phase = $parts[1]
            ready = [bool]::Parse($parts[2])
            restartCount = [int]$parts[3]
            imageDigest = $digestMatch.Value
        }
    }

    $hpaParts = Split-ExactProjection (Invoke-AllowlistedProjection "KubectlHpa") 7 "HPA"
    $hpa = [ordered]@{
        name = $hpaParts[0]
        minimumReplicas = [int]$hpaParts[1]
        maximumReplicas = [int]$hpaParts[2]
        currentReplicas = [int]$hpaParts[3]
        desiredReplicas = [int]$hpaParts[4]
        cpuTargetPercent = [int]$hpaParts[5]
        memoryTargetPercent = [int]$hpaParts[6]
    }

    return [ordered]@{
        schemaVersion = 1
        evidenceType = "live"
        capturedAtUtc = ConvertTo-UtcText ([DateTimeOffset]::UtcNow)
        repository = [ordered]@{
            nameWithOwner = [string]$repository.nameWithOwner
            url = [string]$repository.url
            commitSha = $sha
        }
        deployRun = $deployRun
        api = [ordered]@{ baseUrl = $apiUrl }
        aws = [ordered]@{
            region = "us-east-1"
            eks = [ordered]@{
                clusterName = [string]$eksCluster.clusterName
                status = [string]$eksCluster.status
                version = [string]$eksCluster.version
                nodeGroupName = [string]$eksNodeGroup.nodeGroupName
                nodeGroupStatus = [string]$eksNodeGroup.nodeGroupStatus
            }
            rds = [ordered]@{
                identifier = [string]$rds.identifier
                status = [string]$rds.status
                engine = [string]$rds.engine
                publiclyAccessible = [bool]$rds.publiclyAccessible
            }
            ecr = [ordered]@{
                repositoryName = "garageflow"
                imageTag = $sha
                imageDigest = [string]$ecrImage.imageDigest
            }
            sns = [ordered]@{
                topicName = "garageflow-work-orders"
                status = "confirmed-and-received"
                confirmedEmailSubscriptions = $subscriptions.Count
                notificationObservedAtUtc = ConvertTo-UtcText $SnsNotificationObservedAtUtc
            }
        }
        kubernetes = [ordered]@{
            namespace = "garageflow"
            service = [ordered]@{ name = "garageflow-api"; type = $serviceParts[0] }
            deployment = $deployment
            pods = $pods
            hpa = $hpa
        }
        hpaDemonstration = [ordered]@{
            status = "passed"
            baseline = [ordered]@{ observedAtUtc = ConvertTo-UtcText $HpaBaselineObservedAtUtc; readyReplicas = 2 }
            peak = [ordered]@{ observedAtUtc = ConvertTo-UtcText $HpaPeakObservedAtUtc; maximumReplicasObserved = $HpaPeakReplicas }
            recovery = [ordered]@{ observedAtUtc = ConvertTo-UtcText $HpaRecoveredAtUtc; readyReplicas = 2 }
            k6 = [ordered]@{ httpReqFailedRate = $HttpReqFailedRate; httpReqDurationP95Ms = $HttpReqDurationP95Ms }
        }
    }
}

function Assert-PublicIPAddress {
    param([System.Net.IPAddress]$Address, [string]$Location)

    Assert-Condition ($null -ne $Address) "$Location resolved to an invalid address."
    if ($Address.IsIPv4MappedToIPv6) {
        Assert-PublicIPAddress -Address $Address.MapToIPv4() -Location $Location
        return
    }
    Assert-Condition (-not [System.Net.IPAddress]::IsLoopback($Address)) "$Location resolved to loopback."
    $bytes = $Address.GetAddressBytes()
    if ($Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork) {
        $reserved = $bytes[0] -in @(0, 10, 127) -or $bytes[0] -ge 224 -or
            ($bytes[0] -eq 100 -and $bytes[1] -ge 64 -and $bytes[1] -le 127) -or
            ($bytes[0] -eq 169 -and $bytes[1] -eq 254) -or
            ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) -or
            ($bytes[0] -eq 192 -and $bytes[1] -eq 168) -or
            ($bytes[0] -eq 192 -and $bytes[1] -eq 0 -and $bytes[2] -in @(0, 2)) -or
            ($bytes[0] -eq 198 -and $bytes[1] -in @(18, 19)) -or
            ($bytes[0] -eq 198 -and $bytes[1] -eq 51 -and $bytes[2] -eq 100) -or
            ($bytes[0] -eq 203 -and $bytes[1] -eq 0 -and $bytes[2] -eq 113)
        Assert-Condition (-not $reserved) "$Location resolved to a private, link-local, documentation, or reserved address."
        return
    }
    $reservedV6 = $Address.Equals([System.Net.IPAddress]::IPv6Any) -or
        $Address.Equals([System.Net.IPAddress]::IPv6None) -or
        $Address.IsIPv6LinkLocal -or $Address.IsIPv6SiteLocal -or
        $bytes[0] -eq 0xFF -or (($bytes[0] -band 0xFE) -eq 0xFC) -or
        ($bytes[0] -eq 0x20 -and $bytes[1] -eq 0x01 -and $bytes[2] -eq 0x0D -and $bytes[3] -eq 0xB8)
    Assert-Condition (-not $reservedV6) "$Location resolved to a private, link-local, documentation, or reserved IPv6 address."
}

function Resolve-PublicHostAddresses {
    param([Uri]$Uri, [scriptblock]$Resolver)

    $resolved = @(
        if ($null -eq $Resolver) {
            [System.Net.Dns]::GetHostAddresses($Uri.DnsSafeHost)
        }
        else {
            & $Resolver $Uri.DnsSafeHost
        }
    )
    Assert-Condition ($resolved.Count -gt 0 -and $resolved.Count -le 16) "Video host DNS resolution returned an invalid address count."
    foreach ($entry in $resolved) {
        $address = if ($entry -is [System.Net.IPAddress]) {
            $entry
        }
        else {
            $parsed = $null
            Assert-Condition ([System.Net.IPAddress]::TryParse([string]$entry, [ref]$parsed)) `
                "Video host resolver returned a non-IP value."
            $parsed
        }
        Assert-PublicIPAddress -Address $address -Location "video host '$($Uri.DnsSafeHost)'"
    }
}

function Invoke-AnonymousHeadersRequest {
    param([Uri]$Uri, [ValidateSet("HEAD", "GET")][string]$Method, [scriptblock]$Invoker)

    if ($null -ne $Invoker) {
        $mockResult = & $Invoker $Uri $Method
        Assert-Condition ($null -ne $mockResult -and $mockResult.StatusCode -is [int]) `
            "Mock anonymous HTTP result must contain an integer StatusCode."
        return [pscustomobject]@{
            StatusCode = [int]$mockResult.StatusCode
            Location = [string]$mockResult.Location
        }
    }

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $handler.UseCookies = $false
    $handler.MaxResponseHeadersLength = 32
    $client = [System.Net.Http.HttpClient]::new($handler, $true)
    $client.Timeout = [TimeSpan]::FromSeconds(15)
    $httpMethod = if ($Method -ceq "HEAD") { [System.Net.Http.HttpMethod]::Head } else { [System.Net.Http.HttpMethod]::Get }
    $request = [System.Net.Http.HttpRequestMessage]::new($httpMethod, $Uri)
    if ($Method -ceq "GET") {
        $request.Headers.Range = [System.Net.Http.Headers.RangeHeaderValue]::new(0, 0)
    }
    $response = $null
    try {
        $response = $client.SendAsync(
            $request,
            [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
        return [pscustomobject]@{
            StatusCode = [int]$response.StatusCode
            Location = if ($null -eq $response.Headers.Location) { "" } else { $response.Headers.Location.OriginalString }
        }
    }
    catch {
        throw "Anonymous video reachability request failed."
    }
    finally {
        if ($null -ne $response) { $response.Dispose() }
        $request.Dispose()
        $client.Dispose()
    }
}

function Test-AnonymousVideoReachability {
    param([string]$Url, [scriptblock]$Resolver, [scriptblock]$RequestInvoker)

    $currentUri = Assert-RealUrl -Value $Url -RequireHttps $true -Location "video URL"
    for ($redirectCount = 0; $redirectCount -le 5; $redirectCount++) {
        $currentUri = Assert-RealUrl -Value $currentUri.AbsoluteUri -RequireHttps $true -Location "video URL/redirect"
        Resolve-PublicHostAddresses -Uri $currentUri -Resolver $Resolver
        $result = Invoke-AnonymousHeadersRequest -Uri $currentUri -Method "HEAD" -Invoker $RequestInvoker
        if ($result.StatusCode -in @(405, 501)) {
            $result = Invoke-AnonymousHeadersRequest -Uri $currentUri -Method "GET" -Invoker $RequestInvoker
        }
        if ($result.StatusCode -in @(301, 302, 303, 307, 308)) {
            Assert-Condition ($redirectCount -lt 5) "Anonymous video URL exceeded five redirects."
            Assert-Condition (-not [string]::IsNullOrWhiteSpace($result.Location)) "Anonymous video redirect omitted Location."
            $currentUri = [Uri]::new($currentUri, $result.Location)
            continue
        }
        Assert-Condition ($result.StatusCode -ge 200 -and $result.StatusCode -lt 300) `
            "Anonymous video URL did not return a successful status."
        return
    }
    throw "Anonymous video URL redirect validation did not terminate."
}

function New-FinalEvidence {
    param([object]$LiveEvidence, [string]$EffectiveOutputPath)

    Assert-Condition $AnonymousPlaybackVerified "Finalize requires explicit incognito playback confirmation."
    Assert-Condition $DestroyVerificationPassed "Finalize requires explicit protected destroy verification confirmation."
    Assert-Condition ($StateBucketName -match '^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$') "State bucket input is invalid."
    Assert-Condition ($Participants.Count -gt 0) "At least one participant is required."
    $normalizedParticipants = @()
    foreach ($participant in $Participants) {
        $keys = @($participant.Keys | ForEach-Object { [string]$_ } | Sort-Object)
        Assert-Condition (($keys -join "|") -ceq "identifier|name") "Participants accept only name and identifier."
        $normalizedParticipants += [ordered]@{
            name = ([string]$participant["name"]).Trim()
            identifier = ([string]$participant["identifier"]).Trim()
        }
    }
    $normalizedGroup = $GroupIdentifier.Trim()
    Assert-Condition ($normalizedGroup -match '^[A-Za-z0-9._-]{2,80}$') "Group identifier is invalid."
    foreach ($participant in $normalizedParticipants) {
        Assert-Condition ($participant.name -match '^\S(?:[^\r\n]{0,198}\S)?$') "Participant name is invalid."
        Assert-Condition ($participant.identifier -match '^[A-Za-z0-9._-]{2,80}$') "Participant identifier is invalid."
    }
    $participantIds = @($normalizedParticipants | ForEach-Object { $_.identifier })
    Assert-Condition (@($participantIds | Sort-Object -Unique).Count -eq $participantIds.Count) `
        "Participant identifiers must be unique."
    Assert-NoSensitiveEvidence -Value ([ordered]@{
        group = $normalizedGroup
        participants = $normalizedParticipants
        videoUrl = $VideoUrl
    }) -Location "Finalize local inputs"
    $null = Assert-RealUrl -Value $VideoUrl -RequireHttps $true -Location "video URL"

    $allowedPaths = @(
        (Get-RelativeRepositoryPath $LiveEvidencePath),
        (Get-RelativeRepositoryPath $EffectiveOutputPath),
        "docs/delivery/phase-2-live-evidence.json",
        "docs/delivery/phase-2-evidence.json"
    ) | Sort-Object -Unique
    Assert-RepositoryState -AllowedRelativePaths $allowedPaths
    $sha = (Invoke-AllowlistedProjection "GitHead").Trim()
    Assert-Condition ($sha -ceq [string]$LiveEvidence.repository.commitSha) `
        "Current Git HEAD differs from the demonstrated live commit."
    $repository = Get-RepositoryProjection
    Assert-Condition ([string]$repository.nameWithOwner -ceq [string]$LiveEvidence.repository.nameWithOwner -and
                      ([string]$repository.url).TrimEnd('/') -ceq ([string]$LiveEvidence.repository.url).TrimEnd('/')) `
        "Current repository differs from the live evidence repository."

    $qualityRun = ConvertFrom-WorkflowProjection `
        -Text (Invoke-AllowlistedProjection "GhQualityRun" @{ Sha = $sha }) `
        -WorkflowName "Quality Gate" `
        -Location "quality workflow"
    $destroyRun = ConvertFrom-WorkflowProjection `
        -Text (Invoke-AllowlistedProjection "GhDestroyRun" @{ Sha = $sha }) `
        -WorkflowName "Destroy AWS Academy" `
        -Location "destroy workflow"

    $null = Assert-AwsIdentityProjection
    $bucketContext = @{ BucketName = $StateBucketName }
    $null = Invoke-AllowlistedProjection "AwsBucketHead" $bucketContext
    $versioning = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsBucketVersioning" $bucketContext) `
        -ExpectedProperties @("status") `
        -Location "retained bucket versioning"
    $publicAccess = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsBucketPublicAccess" $bucketContext) `
        -ExpectedProperties @("blockPublicAcls", "ignorePublicAcls", "blockPublicPolicy", "restrictPublicBuckets") `
        -Location "retained bucket public access"
    $encryption = ConvertFrom-ProjectionJson `
        -Text (Invoke-AllowlistedProjection "AwsBucketEncryption" $bucketContext) `
        -ExpectedProperties @("algorithm") `
        -Location "retained bucket encryption"
    $allPublicBlocks = (Assert-JsonBooleanTrue $publicAccess.blockPublicAcls "retained bucket blockPublicAcls") -and
        (Assert-JsonBooleanTrue $publicAccess.ignorePublicAcls "retained bucket ignorePublicAcls") -and
        (Assert-JsonBooleanTrue $publicAccess.blockPublicPolicy "retained bucket blockPublicPolicy") -and
        (Assert-JsonBooleanTrue $publicAccess.restrictPublicBuckets "retained bucket restrictPublicBuckets")
    Assert-Condition ([string]$versioning.status -ceq "Enabled" -and $allPublicBlocks) `
        "Retained bucket must remain private and versioned."
    Assert-Condition ([string]$encryption.algorithm -in @("AES256", "aws:kms")) `
        "Retained bucket encryption is not approved."

    $destroyCompletedAt = ConvertFrom-StrictUtcText ([string]$destroyRun.completedAtUtc) "destroy workflow completedAtUtc"
    $videoVerifiedAt = $VideoVerifiedAtUtc.ToUniversalTime()
    Assert-Condition ($destroyCompletedAt -le $videoVerifiedAt -and $videoVerifiedAt -le [DateTimeOffset]::UtcNow) `
        "Anonymous video verification timestamp must follow destroy completion and cannot be in the future."

    Test-AnonymousVideoReachability -Url $VideoUrl

    $verifiedNow = ConvertTo-UtcText ([DateTimeOffset]::UtcNow)
    return [ordered]@{
        schemaVersion = 1
        evidenceType = "final"
        finalizedAtUtc = $verifiedNow
        group = [ordered]@{ identifier = $normalizedGroup }
        participants = $normalizedParticipants
        video = [ordered]@{
            url = $VideoUrl
            visibility = $VideoVisibility
            durationSeconds = $VideoDurationSeconds
            anonymousPlaybackVerified = $true
            verifiedAtUtc = ConvertTo-UtcText $VideoVerifiedAtUtc
        }
        liveEvidence = $LiveEvidence
        qualityGateRun = $qualityRun
        destroy = [ordered]@{
            status = "passed"
            run = $destroyRun
            verifiedAtUtc = $verifiedNow
            checks = [ordered]@{
                eksClusterAbsent = $true
                nodeGroupAbsent = $true
                runningEc2WorkersAbsent = $true
                rdsAbsent = $true
                ecrAbsent = $true
                snsAbsent = $true
                configurationEntriesAbsent = $true
                loadBalancerAbsent = $true
                vpcAbsent = $true
            }
        }
        retainedBackendBucket = [ordered]@{
            status = "retained-private-versioned"
            accessible = $true
            versioningStatus = [string]$versioning.status
            publicAccessBlocked = $allPublicBlocks
            encryption = [string]$encryption.algorithm
            verifiedAtUtc = $verifiedNow
        }
    }
}

$resolvedSchemaPath = Assert-CanonicalDeliveryPath `
    -Candidate $SchemaPath `
    -ExpectedFileName "phase-2-evidence.schema.json" `
    -MustExist
$schemaItem = Get-Item -LiteralPath $resolvedSchemaPath
Assert-Condition ($schemaItem.Length -gt 0 -and $schemaItem.Length -le 1MB) "Evidence schema size is invalid."
$null = Get-Content -LiteralPath $resolvedSchemaPath -Raw | ConvertFrom-Json -Depth 100

if ($ValidateOnly) {
    $validated = Read-Evidence -Path $EvidencePath -EffectiveSchemaPath $resolvedSchemaPath -ExpectedType ""
    Write-Host "Validated $($validated.evidenceType) evidence fixture."
    return
}

$expectedOutputFileName = if ($CaptureLive) { "phase-2-live-evidence.json" } else { "phase-2-evidence.json" }
$effectiveOutputPath = Assert-CanonicalDeliveryPath -Candidate $OutputPath -ExpectedFileName $expectedOutputFileName
$resolvedLiveEvidencePath = if ($Finalize) {
    Assert-CanonicalDeliveryPath `
        -Candidate $LiveEvidencePath `
        -ExpectedFileName "phase-2-live-evidence.json" `
        -MustExist
}
else { "" }

$action = if ($CaptureLive) { "Capture validated live Phase 2 evidence" } else { "Finalize validated Phase 2 evidence" }
if (-not $PSCmdlet.ShouldProcess($effectiveOutputPath, $action)) { return }
Assert-OutputAvailable -Path $effectiveOutputPath -AllowOverwrite:$Force

if ($CaptureLive) {
    $evidence = New-LiveEvidence -EffectiveOutputPath $effectiveOutputPath
}
else {
    $live = Read-Evidence -Path $resolvedLiveEvidencePath -EffectiveSchemaPath $resolvedSchemaPath -ExpectedType "live"
    $evidence = New-FinalEvidence -LiveEvidence $live -EffectiveOutputPath $effectiveOutputPath
}

Write-AtomicEvidence -Evidence $evidence -Path $effectiveOutputPath -EffectiveSchemaPath $resolvedSchemaPath -AllowOverwrite:$Force
Write-Host "Wrote validated $($evidence.evidenceType) evidence atomically to $effectiveOutputPath"
