<#
    This script performs the following administrative tasks on AD
    1. Removes disabled machines from AD
    2. Moves machines with last login > 365 days to a separte OU
    3. Outputs the list of files to disk

    Please note that this needs to run on an account that has access rights to the OUs and Computer objects
#>

Import-Module ActiveDirectory; 

<# Configurable parameters #>
$Cleanup_RemoveDisabledADComputers = $True
$Cleanup_MoveLastLoginADComputers = $True
$LastLoginMoreThanDays = 210
$SearchBaseOU = "OU=All,OU=computers,DC=department,DC=domain,DC=com"
$LastLoginMoveToOU = "OU=PendingDelete,OU=computers,DC=department,DC=domain,DC=com"

$smtpserver = "mail.domain.com"
$emailFrom = "sysadmin@domain.com"
$emailTo = "sysadmin@domain.com"
$subject = "Old computers in Active Directory"
$body = "Please refer to attachment"

<# Program proper, do not modify anything from this line onwards #>
$emailToActual = "$emailTo"

$ExpiryDate = (Get-Date).Adddays(-$LastLoginMoreThanDays)

$DisabledCompsCSV = Join-Path "$(Get-Location)" "$((Get-Date).ToString("yyyy-MM-dd HH-mm-ss"))_DisabledComputers.csv"
$LastLoginCompsCSV = Join-Path "$(Get-Location)" "$((Get-Date).ToString("yyyy-MM-dd HH-mm-ss"))_LastLoginComputers.csv"
$subject = $subject + " dtd " + (Get-Date).ToString("yyyy-MM-dd HH-mm-ss")

if($Cleanup_RemoveDisabledADComputers)
{
    Write-Output "#####################"
    Write-Output "Disabled Computers"
    Write-Output "#####################"
    Write-Output "Output CSV: $DisabledCompsCSV"
    
    #Get list of disabled computers in LKC AD
    $DisabledCompsInAD = Search-ADAccount -ComputersOnly -AccountDisabled -SearchBase $SearchBaseOU
    Write-Output "Computers Found: $($DisabledCompsInAD.Count)"

    #Export the list into a CSV file
    $DisabledCompsInAD | Get-ADComputer -Properties Description,OperatingSystem,ipv4Address,lastlogontimestamp, whencreated |
        Select-Object @{n="Computer";e={$_.Name}}, @{Name="Lastlogon";Expression={[DateTime]::FromFileTime($_.lastLogonTimestamp)}},whencreated,
        Description, DNSHostName, DistinguishedName, Enabled, ipv4Address,OperatingSystem, ObjectClass, ObjectGuid, SID, UserPrincipalName | Export-CSV $DisabledCompsCSV -NoTypeInformation

    #Remove the disabled computers
    $DisabledCompsInAD | Remove-ADObject -Confirm:$False -Recursive

    if ($DisabledCompsInAD.Count -gt 0)
    {
        Write-Output "Sending email for Disabled Computers";
        Send-MailMessage -To $emailToActual -From $emailFrom -Subject $subject -Body $body -Attachments $DisabledCompsCSV -SmtpServer $smtpserver
    }
}

if ($Cleanup_MoveLastLoginADComputers)
{
    Write-Output "#####################"
    Write-Output "Last Login > Computers"
    Write-Output "#####################"
    Write-Output "Output CSV: $LastLoginCompsCSV"

    #Get list of disabled computers in LKC AD
    $LastLoginInAD = Get-ADComputer -SearchBase $SearchBaseOU -Filter {lastlogontimestamp -lt $ExpiryDate -and enabled -eq $true} -Properties LastLogon, description,lastlogontimestamp
    Write-Output "Computers Found: $($LastLoginInAD.Count)"
    
    #Export the list into a CSV file
    $LastLoginInAD |Select-Object @{n="Computer";e={$_.Name}}, @{Name="Lastlogon";Expression={[DateTime]::FromFileTime($_.lastlogontimestamp)}},whencreated,
        Description, DNSHostName, DistinguishedName, Enabled, ipv4Address,OperatingSystem, ObjectClass, ObjectGuid, SID, UserPrincipalName | Export-CSV $LastLoginCompsCSV -NoTypeInformation

    $LastLoginInAD | Move-ADObject -TargetPath $LastLoginMoveToOU

    if ($LastLoginInAD.Count -gt 0)
    {
        Write-Output "Sending email for Last Login Computers";
        Send-MailMessage -To $emailToActual -From $emailFrom -Subject $subject -Body $body -Attachments $LastLoginCompsCSV -SmtpServer $smtpserver
    }
}
