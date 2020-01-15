# SQRL Dot Net Core Client and Library
This project has 2 main parts a SQRL library and a SQRL Client below is information on both.

[SQRL Dot Net Core Library](#SQRL-Dot-Net-Core-Library)

[SQRL Dot Net Core Client](#SQRL-Dot-Net-Core-Client)

### SQRL Dot Net Core Library

An implementation of the full client protocol for SQRL written in Dot Net Core fully cross-platform to (Win, Nix, Mac)

![SQRLClientDemo](/SQRLUtilsLib/Resources/SQRLClientDemo.gif)

#### How to Install

`Install-Package SQRLClientLib` 

#### Requirements

This is a Dot Net Core 3.1 library so you will need a compatible project

#### How to Use

##### Create an Instance of the SQRLib class

```csharp

/* 
Create an Instance of the SQRL library
the boolean here tells the library to start the CPS server.
If no CPS server is desired, pass false
*/ 
SQRL sqrlLib = new SQRLUtilsLib.SQRL(true); 
```

##### Create a new SQRL Identity (from scratch)

```csharp
//Creates a new Identity Object
SQRLIdentity newIdentity = new SQRLUtilsLib.SQRLIdentity();

//Generates a Identity Unlock Key
var iuk = sqrlLib.CreateIUK();

// Generaties a Rescue Code
var rescueCode = sqrlLib.CreateRescueCode();

// Used to Report Progress when Encrypting / Decrypting (progress bar maybe)
var progress = new Progress<KeyValuePair<int, string>>(percent =>
{
	Console.WriteLine($"{percent.Value}: {percent.Key}%");
});

newIdentity = await sqrlLib.GenerateIdentityBlock1(iuk, "My-Awesome-Password", newIdentity, progress);

newIdentity = await sqrlLib.GenerateIdentityBlock2(iuk, rescueCode, newIdentity, progress);
```
##### Import Identity From File

```csharp
SQRLIdentity newIdentity=SQRL.ImportSqrlIdentityFromFile(@"C:\Temp\identiy.sqrl");
```



##### Import Identity from Text

```csharp
//Creates a new Identity Object
string identityTxt = "KKcC 3BaX akxc Xwbf xki7 k7mF GHhg jQes gzWd 6TrK vMsZ dBtB pZbC zsz8 cUWj DtS2 ZK2s ZdAQ 8Yx3 iDyt QuXt CkTC y6gc qG8n Xfj9 bHDA 422";

string rescueCode = "119887487132283883187570";

string password = "Zingo-Bingo-Slingo-Dingo";        

//Reports progress while decrypting / encrypting the identity
var progress = new Progress<KeyValuePair<int, string>>(percent =>
{
	Console.WriteLine($"{percent.Value}: {percent.Key}%");
});

// Decodes the identity from Text Import
SQRLIdentity newIdentity = await sqrlLib.DecodeSqrlIdentityFromText(identityTxt, rescueCode, password, progress);
```
##### Export Identity to File

```csharp
newIdentity.WriteToFile(@"C:\Temp\My-SQRL-Identity.sqrl");
```

##### Re-Key Identity

```csharp
//Have an existing Identity Object (somehow)
SQRLUtilsLib.SQRLIdentity existingIdentity = SQRL.ImportSqrlIdentityFromFile(@"C:\Temp\MyCurrentIdentity.sqrl");        

//Reports progress while decrypting / encrypting the identity it is optional
var progress = new Progress<KeyValuePair<int, string>>(percent =>
{
	Console.WriteLine($"{percent.Value}: {percent.Key}%");
});
//Re-Keys the existing Identity object and returns a tuple of your new rescue code and the new Identity (which now contains a new entry in block3 )
var reKeyResponse = await sqrlLib.RekeyIdentity(existingIdentity, rescueCode, "My-New-Even-Better-Password", progress); 

Console.WriteLine($"New Rescue Code: {reKeyResponse.Key}");

var NewlyReKeyedIdentity = reKeyResponse.Value;
```


##### Generate a Site Key Pair

```csharp
SQRLUtilsLib.SQRL sqrlLib = new SQRLUtilsLib.SQRL();
//Reports progress while decrypting / encrypting the identity
var progress = new Progress<KeyValuePair<int, string>>(percent =>
{
	Console.WriteLine($"{percent.Value}: {percent.Key}%");
});
//Have an existing identity (some-how)
SQRLIdentity existingIdentity = SQRL.ImportSqrlIdentityFromFile(@"C:\Temp\MyCurrentIdentity.sqrl");

//Returns a tuple of 3 values a boolean indicating sucess, Item2 = IMK Item3 = ILK
var block1DecryptedData= await sqrlLib.DecryptBlock1(existingIdentity, "My-Awesome-Password", progress);
/*
Note that bloc1DecryptedData returns a tuple as mentioned above 
Item1 is a  boolean (sucess/not)
Item2 is (IMK) Identity Master Key
Item3 is (ILK) Identity Lock Key
*/
if (block1DecryptedData.Item1) //If Sucess
{
    //This is the site's Key Pair for signing requests
	Sodium.KeyPair siteKP = sqrlLib.CreateSiteKey(new Uri("sqrl://sqrl.grc.com/cli.sqrl?nut=fXkb4MBToCm7"), "Alt-ID-If-You-Want-One", block1DecryptedData.Item2); //Item2=IMK
}
else
	throw new Exception("Invalid password, failed to decrypt");
```


##### Generate a Query Command to the Server

Assumes you have a valid SiteKeyPair

```csharp
//SQRL url
Uri sqrlUrl = new Uri("sqrl://sqrl.grc.com/cli.sqrl?nut=fXkb4MBToCm7");
//SQRL client options include CPS, SUK, HARDLOCK, NOIPTEST,SQRLONLY
SQRLOptions opts = new SQRLOptions(SQRLOptions.SQRLOpts.CPS | SQRLOptions.SQRLOpts.SUK | SQRLOptions.SQRLOpts.);            
/*
Generates a query command and sends it to the server, requires that you have a  valid site keypair
returns a "SQRLServerResponse" object which contains all pertinent data of the response from the server
              
*/
SQRLServerResponse sqrlResponse = sqrlLib.GenerateQueryCommand(sqrlUrl, siteKP, opts);
```


##### Deal with Ask on Query Response



```csharp
if (serverRespose.HasAsk) //Returns true if server sent Ask
{
	Console.WriteLine(serverRespose.AskMessage);
	Console.WriteLine($"Enter 1 for {serverRespose.GetAskButtons[0]} or 2 for 	{serverRespose.GetAskButtons[1]}");
	int resp;
do
{
    string response = Console.ReadLine();
    int.TryParse(response, out resp);
    if (resp == 0)
    {
    Console.WriteLine("Invalid Entry, please enter 1 or 2 as shown above");
    }

} while (resp == 0);
askResponse = resp;

}

StringBuilder addClientData = null;
if (askResponse > 0)
{
    addClientData = new StringBuilder();
    addClientData.AppendLineWindows($"btn={askResponse}");
}
// addClientData now needs to be passed in to the next command (Ident)
```
##### Generate Ident (create) Command

Assumes you have a generated SiteKeyPair
Assumes you have a decrypted ILK (Identity Lock Key) (by decrypting block1)

```csharp
if (!serverRespose.CurrentIDMatch) //New Account
{
    //Generates the SUK / VUK from ILK and RLK (Random Lock Key)
    var sukvuk = sqrl.GetSukVuk(decryptedData.Item3); // ILK from Decrypted Block1
    SQRL.ZeroFillByteArray(decryptedData.Item3); // Clear ILK from memory because we need to be good citizens

    //builds the special client data that's required for this command VUK/SUK
    StringBuilder addClientData = new StringBuilder(); // If Ask exists don't forget to append it to this too (as shown above)

    addClientData.AppendLineWindows($"suk={Sodium.Utilities.BinaryToBase64(sukvuk.Key, Sodium.Utilities.Base64Variant.UrlSafeNoPadding)}");
    addClientData.AppendLineWindows($"vuk={Sodium.Utilities.BinaryToBase64(sukvuk.Value, Sodium.Utilities.Base64Variant.UrlSafeNoPadding)}");                                

    //Calls the Ident command, notice we are passing the prior (Query's) serverResponse.NewNutURL
    serverRespose = sqrl.GenerateCommand(serverRespose.NewNutURL, siteKvp, serverRespose.FullServerRequest, "ident", opts, addClientData);
}
```

****



##### Send Enable Command



```csharp
if (serverRespose.SQRLDisabled)
{
    Console.WriteLine("SQRL Is Disabled, to Continue you must enable it. Do you want to? (Y/N)");
    if (Console.ReadLine().StartsWith("Y", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Enter your Rescue Code (No Sapces or Dashes)");
        string rescueCode = Console.ReadLine().Trim();
        progress = new Progress<KeyValuePair<int, string>>(percent =>
		{
			Console.WriteLine($"Decrypting with Rescue Code: {percent.Key}%");
		});
        // Decrypts Block2 to generate URS
        var iukData = await sqrl.DecryptBlock2(newId, rescueCode, progress);
        if (iukData.Item1)
        {
            byte[] ursKey = null;
            ursKey = sqrl.GetURSKey(iukData.Item2, Sodium.Utilities.Base64ToBinary(serverRespose.SUK, string.Empty, Sodium.Utilities.Base64Variant.UrlSafeNoPadding));
            SQRL.ZeroFillByteArray(iukData.Item2);
            //Send Enable Command
            serverRespose = sqrl.GenerateCommandWithURS(serverRespose.NewNutURL, siteKvp, ursKey, serverRespose.FullServerRequest, "enable", opts, null);
        }
        else
        {
            throw new Exception("Failed to Decrypt Block 2, Invalid Rescue Code");
        }
    }
}
```
##### Send Disable Command

```csharp
Console.WriteLine("This will disable all use of this SQRL Identity on the server, are you sure you want to proceed?: (Y/N)");
if (Console.ReadLine().StartsWith("Y", StringComparison.OrdinalIgnoreCase))
{
    //Send Disable Command
    serverRespose = sqrl.GenerateCommand(serverRespose.NewNutURL, siteKvp, serverRespose.FullServerRequest, "disable", opts, addClientData);
}
```



##### Send Remove Command

```csharp
Console.WriteLine("Enter your Rescue Code (No Sapces or Dashes)");
string rescueCode = Console.ReadLine().Trim();
progress = new Progress<KeyValuePair<int, string>>(percent =>
{
	Console.WriteLine($"Decrypting with Rescue Code: {percent.Key}%");
});

//Decrypt block2 with rescueCode
var iukData = await sqrl.DecryptBlock2(newId, rescueCode);

if (iukData.Item1) //If all Good
{
    byte[] ursKey = sqrl.GetURSKey(iukData.Item2, Sodium.Utilities.Base64ToBinary(serverRespose.SUK, string.Empty, Sodium.Utilities.Base64Variant.UrlSafeNoPadding));
    SQRL.ZeroFillByteArray(iukData.Item2);
    serverRespose = sqrl.GenerateCommandWithURS(serverRespose.NewNutURL, siteKvp, ursKey, serverRespose.FullServerRequest, "remove", opts, null);
}
else
    throw new Exception("Failed to Decrypt Block 2, Invalid Rescue Code");
```



##### Send Ident and Deal with CPS

Any serverResponse can be dealt with via CPS, if CPS is enabled and has a "pendingRequest"

```csharp
//Send Ident Command
serverRespose = sqrl.GenerateCommand(serverRespose.NewNutURL, siteKvp, serverRespose.FullServerRequest, "ident", opts, addClientData);
if (sqrl.cps != null && sqrl.cps.PendingResponse) //If CPS is running and has a PendingRequest
{
    //If we were successful with our Ident
    if(!sqrl.CommandFailed)
    {
    	sqrl.cps.cpsBC.Add(new Uri(serverRespose.SuccessUrl)); //Redirect to success URI
    }
    else
        sqrl.cps.cpsBC.Add(sqrl.cps.Can); //Redirect to Cancel URI
}
```

### SQRL Dot Net Core Client

An implementation of a full SQRL client along with a cross-platform UI using Avalonia

//TODO Write it and Document it