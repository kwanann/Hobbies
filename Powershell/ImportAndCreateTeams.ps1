<#
Automatic creation of Teams, channels, owners and member based on CSV file

Script is intentionally verbose and spits out any error encountered

Echo's the Team's GroupID for reference

To remove a Team, use: Remove-Team -GroupId ffffffff-ffff-ffff-ffff-fffffffffff

To use this script
1. Create an input CSV file with the following field/format
    Note that TeamAlias must start with a-z and it can only contain a-z0-9 and -

TeamsAlias,TeamsName,TeamType,ChannelName,Owners,Members
MyTeam,My Team Display Name,Private,Channel1;Channel2,owner1@a.onmicrosoft.com;owner2@a.onmicrosoft.com,user1@a.onmicrosoft.com;user2@a.onmicrosoft.com;user3@a.onmicrosoft.com;

2. Edit the very last line of this PS script and change the ImportPath to the path of the CSV created in #1
Create-NewTeam -ImportPath "D:\NewTeams.csv"
#>


<#
    Generic function to add USERS to Teams
#>
function Add-Users
{   
    param(   
             $Users,$GroupId,$Role
          )   
    Process
    {
        
        $Users.Split(";") | ForEach {
            #Trim spaces from the UPN
            $UPN = $_.Trim();

            #Check and ensure UPN is not empty
            If([string]::IsNullOrWhitespace($UPN))
            {
                #Empty UPN, no action
            }
            else
            {                
                Write-host "- " $Role ": " $UPN
                #Add User to Team
                Add-TeamUser -User $UPN -GroupId $GroupId -Role $Role
            }
        }
    }       
}

<#
    Main function to create new team based on CSV Inputs
#>
function Create-NewTeam
{   
   param (   
             $ImportPath
         )   
  Process
    {
        #Import Microsoft Teams functions
        Import-Module MicrosoftTeams

        Write-Host "-------------------"
        Write-Host "Login to Teams with your O365 account"
        Write-Host "-------------------"
        Write-Host ""

        #Use this option if you want to hardcode the login details, only works if there is no 2FA
        #$username = "abc@a.onmicrosoft.com"
        #$password = ConvertTo-SecureString "[thepassword]" -AsPlainText -Force
        #$cred = new-object -typename System.Management.Automation.PSCredential -argumentlist $username, $password
        #Connect-MicrosoftTeams -Credential $cred

        #Option 2: Prompt for login
        Connect-MicrosoftTeams 

        Write-Host "-------------------"
        Write-Host "Importing CSV: " $ImportPath
        Write-Host "-------------------"
        Write-Host ""
        $teams = Import-Csv -Path $ImportPath

        #Assumption: Team ID are all unique. Otherwise will throw error later on

        Write-Host "-------------------"
        Write-Host "Looping thru CSV rows"
        Write-Host "-------------------"
        Write-Host ""
        
        $sw = [system.diagnostics.stopwatch]::StartNew()

        foreach($team in $teams)
        {
            Write-Host "-------------------"
            Write-Host "Processing: " $team

            $sw2 = [system.diagnostics.stopwatch]::StartNew()

            $TeamAlias = $team.TeamsAlias.Trim()
            $TeamDisplayName = $team.TeamsName.Trim()
            $TeamVisibility = $team.TeamType.Trim()
            
            #Create the Team
            Write-Host "New-Team: [ID: " $TeamAlias "][Name: " $TeamDisplayName "][Visibility: " $TeamVisibility "]"
            $group = New-Team -MailNickName $TeamAlias -displayname $TeamDisplayName -Visibility $TeamVisibility
            
            #Output the GroupID
            Write-Host "Team GroupID: " $group.GroupId
            
            #Proceed only if group is sucessfully created
            if ($group)
            {                
                Write-Host ""
                Write-Host "Creating channels [" $team.ChannelName "]"

                $team.ChannelName.Split(";") | ForEach {
                    $ChannelName = $_.Trim();

                    If([string]::IsNullOrWhitespace($ChannelName))
                    {
                    }
                    else
                    {
                        Write-host "- Channel: " $ChannelName
                        New-TeamChannel -DisplayName $ChannelName -GroupId $group.GroupId
                    }
                }

                Write-Host ""
                Write-Host "Add Team Owners [ " $team.Owners "]"
                Add-Users -Users $team.Owners -GroupId $group.GroupId -Role Owner

                Write-Host ""
                Write-Host "Adding team members [" $team.Members "]"
                Add-Users -Users $team.Members -GroupId $group.GroupId -Role Member
                
                $team=$null
            }

            $sw2.Stop()
            Write-Host "Time taken: " $sw2.Elapsed.TotalSeconds
            Write-Host "-------------------"
            Write-Host ""
        }

        $sw.Stop()
        Write-Host "Time taken to create ALL Teams: " $sw.Elapsed.TotalSeconds
    }
}

Create-NewTeam -ImportPath "D:\NewTeams.csv"
