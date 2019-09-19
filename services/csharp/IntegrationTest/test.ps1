param (
	[bool] $rebuild = $true,
	[switch] $test_party,
	[switch] $test_invite,
	[switch] $test_matchmaking,
	[switch] $test_playfab_auth,
	[switch] $test_performance,
	[switch] $test_all,
	[switch] $wait
)

$ErrorActionPreference = "stop"

function Build-Images([String[]] $images) {
	Push-Location "../.."
	try {
		ForEach ($image in $images) {
			Write-Output "Building Docker image for ${image}."
			& "docker.exe" build --file "docker/${image}/Dockerfile" --tag "improbable-onlineservices-${image}:test" --build-arg CONFIG="Debug" .
		}
	} finally {
		Pop-Location
	}
}

function Finish() {
	$exit = $LASTEXITCODE
	# Stops and removes all containers.
	& "docker-compose.exe" -f docker_compose.yml down
	& "docker-compose.exe" -f docker_compose.yml rm --force
	Exit $exit
}

if ($null -eq $Env:SPATIAL_REFRESH_TOKEN) {
	Write-Error "The variable SPATIAL_REFRESH_TOKEN is required."
	Exit 1
}
if ($null -eq $Env:SPATIAL_PROJECT) {
	Write-Error "The variable SPATIAL_PROJECT is required."
	Exit 1
}
if ($null -eq $Env:PLAYFAB_SECRET_KEY) {
	Write-Error "The variable PLAYFAB_SECRET_KEY is required."
	Exit 1
}

if ($test_all) {
	$test_party = $true
	$test_invite = $true
	$test_matchmaking = $true
	$test_playfab_auth = $true
}

if ($rebuild) {
	Build-Images @("gateway","gateway-internal","test-matcher","party","playfab-auth")
}

if ($null -eq $Env:TEST_RESULTS_DIR) {
	# Just log to current directory if there's no environment variable
	$Env:TEST_RESULTS_DIR = "."
}

try {
	# Create containers, and then start them backgrounded.
	& "docker-compose.exe" -f docker_compose.yml up --no-start
	& "docker-compose.exe" -f docker_compose.yml start
	
	if ($test_matchmaking) {
		Write-Output "Running tests for the Matchmaking system."
		& "dotnet.exe" test --filter "MatchmakingSystemShould" --logger:"nunit;LogFilePath=$Env:TEST_RESULTS_DIR\MatchmakingSystem.Integration.Test.xml"
	}
	
	if ($test_party) {
		Write-Output "Running tests for the Party system."
		& "dotnet.exe" test --filter "PartySystemShould" --logger:"nunit;LogFilePath=$Env:TEST_RESULTS_DIR\PartySystem.Integration.Test.xml"
	}
	
	if ($test_invite) {
		Write-Output "Running tests for the Invite system."
		& "dotnet.exe" test --filter "InviteSystemShould" --logger:"nunit;LogFilePath=$Env:TEST_RESULTS_DIR\/InviteSystem.Integration.Test.xml"
	}

	if ($test_playfab_auth) {
		Write-Output "Running tests for the PlayFab Auth system."
		& "dotnet.exe" test --filter "PlayFabAuthShould" --logger:"nunit;LogFilePath=$Env:TEST_RESULTS_DIR\PlayFabAuth.Integration.Test.xml"
	}

	if ($test_performance) {
		Write-Output "Running Performance tests."
		& "dotnet.exe" test --filter "GatewayPerformanceShould" --logger:"nunit;LogFilePath=$Env:TEST_RESULTS_DIR\GatewayPerformance.Integration.Test.xml"
	}
    
	if ($wait) {
		Write-Output "Services started. Waiting for user input before quitting."
		& "docker-compose.exe" -f docker_compose.yml logs -f
	}
} finally {
	Finish
}
