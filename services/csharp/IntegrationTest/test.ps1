param (
	[bool] $rebuild = $true,
	[switch] $test_party,
	[switch] $test_invite,
	[switch] $test_matchmaking,
	[switch] $test_all
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

if ($test_all) {
	$test_party = $true
	$test_invite = $true
	$test_matchmaking = $true
}

if ($rebuild) {
	Build-Images @("gateway","gateway-internal","test-matcher","party")
}

try {
	# Create containers, and then start them backgrounded.
	& "docker-compose.exe" -f docker_compose.yml up --no-start
	& "docker-compose.exe" -f docker_compose.yml start
	
	if ($test_matchmaking) {
		Write-Output "Running tests for the Matchmaking system."
		dotnet test --filter "MatchmakingSystemShould"
	}
	
	if ($test_party) {
		Write-Output "Running tests for the Party system."
		& "dotnet.exe" test --filter "PartySystemShould"
	}
	
	if ($test_invite) {
		Write-Output "Running tests for the Invite system."
		& "dotnet.exe" test --filter "InviteSystemShould"
	}
} finally {
	Finish
}