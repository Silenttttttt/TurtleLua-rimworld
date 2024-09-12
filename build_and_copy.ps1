# Navigate to the project directory
cd "G:\SteamLibrary\steamapps\common\RimWorld\Mods\TurtleBotSimplified\TurtleMod"

# Initialize a variable to capture if the build was successful
$buildSuccess = $false

try {
    # Build the project
    dotnet build

    # Check if the build was successful
    if ($?) {
        Write-Host "Build successful. Copying DLL..."
        $buildSuccess = $true

        $sourcePath = ".\bin\Debug\net472\TurtleMod.dll"
        
        # Set destination path one folder above the current directory
        $destinationPath = "..\Assemblies\TurtleMod.dll"

        # Function to copy the DLL with retries
        function Copy-DLLWithRetry {
            param (
                [string]$source,
                [string]$destination,
                [int]$retryCount = 5,
                [int]$delaySeconds = 2
            )

            for ($i = 0; $i -lt $retryCount; $i++) {
                try {
                    Copy-Item -Path $source -Destination $destination -Force
                    Write-Host "DLL copied successfully."
                    return
                } catch [System.IO.IOException] {
                    Write-Host "DLL is locked. Retrying in $delaySeconds seconds..."
                    Start-Sleep -Seconds $delaySeconds
                }
            }
            Write-Host "Failed to copy DLL after $retryCount attempts."
        }

        # Call the function to copy the DLL
        Copy-DLLWithRetry -source $sourcePath -destination $destinationPath
    } else {
        Write-Host "Build failed. Please check the errors."
    }
} finally {
    # Always navigate back to the parent directory
    cd "G:\SteamLibrary\steamapps\common\RimWorld\Mods\TurtleBotSimplified\"
}
