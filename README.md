# PEDCalc
[![Version](https://img.shields.io/github/release/rookiestyle/pedcalc)](https://github.com/rookiestyle/pedcalc/releases/latest)
[![Releasedate](https://img.shields.io/github/release-date/rookiestyle/pedcalc)](https://github.com/rookiestyle/pedcalc/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/rookiestyle/pedcalc/total?color=%2300cc00)](https://github.com/rookiestyle/pedcalc/releases/latest/download/PEDCalc.plgx)\
[![License: GPL v3](https://img.shields.io/github/license/rookiestyle/pedcalc)](https://www.gnu.org/licenses/gpl-3.0)

Did you ever change a password in KeePass and forgot to adjust the expiry date?  
Did you ever wonder how to accompany for this tedious 'x days' password change rule in your company?  
PEDCalc will help you.

PEDCalc let's you define a *lifetime* (validity period) for passwords and will automatically update the expiry time if you change an entry's password.

# Table of Contents
- [Configuration](#configuration)
- [Usage](#usage)
- [Translations](#translations)
- [Download and Requirements](#download-and-requirements)

# Configuration
There is no complex configuration, PEDCalc is designed to *simply work*.  
PEDCalc is either active or not, use the *Tools* menu to activate/deactivate it.

# Usage
PEDCalc integrates into the context menu as well as into the main menu.  
You can use it for both groups and entries.  
A setting defined on group level will be valid for all entries within this group and its subgroups.  
<img src="images/PEDCalc%20context%20menu.png" width="50%" height="50%" alt="Context menu integration" />  

PEDCalc uses the above defined setting to automatically recalculate the expiry date whenver you change an entry's password.  
The calculated new expiry is shown to you in the *Edit entry* form already before saving and you can manually change it to whatever date you want.  
<img src="images/PEDCalc%20password%20change.png" width="50%" height="50%" alt="Password change" />

## Example 1
You want to change passwords every 6 months but there are some bank accounts where you're forced to change the password every 30 days.  
Only two PEDCalc settings are required for this:  
- Define *6 months* lifetime for your database' rootgroup - this will make it effective for the entire db (including your bank accounts)
- Define *30 days* for the group containing your bank acccounts - this will overrule the general setting
- Alternatively, you can also define the lifetime on entry level

## Example 2
Let's assume today's date is jan 1st, 2018 and your database consists of entries A, B, C and D
Entry | Lifetime | Expiry date changed too? | New expiry
------|--------|------|------
A | Off | N/A | No change
B | 2 months | No | March 1st
C | 100 days| No | April 11th
D | 100 days| Yes | Whatever you set manually

# Translations
PEDCalc is provided with English language built-in and allow usage of translation files.
These translation files need to be placed in a folder called *Translations* inside in your plugin folder.
If a text is missing in the translation file, it is backfilled with English text.
You're welcome to add additional translation files by creating a pull request as described in the [wiki](https://github.com/Rookiestyle/PEDCalc/wiki/Create-or-update-translations).

Naming convention for translation files: `<plugin name>.<language identifier>.language.xml`\
Example: `PEDCalc.de.language.xml`
  
The language identifier in the filename must match the language identifier inside the KeePass language that you can select using *View -> Change language...*\
This identifier is shown there as well, if you have [EarlyUpdateCheck](https://github.com/rookiestyle/earlyupdatecheck) installed

# Download and Requirements
## Download
Please follow these links to download the plugin file itself.
- [Download newest release](https://github.com/rookiestyle/pedcalc/releases/latest/download/PEDCalc.plgx)
- [Download history](https://github.com/rookiestyle/pedcalc/releases)

If you're interested in any of the available translations in addition, please download them from the [Translations](Translations) folder.
## Requirements
* KeePass: 2.39
* .NET framework: 3.5