# FolderGram

Using Avalonia UI, WTelegramClient and xFFMPEG.Net packages.

Upload all files in the folder to selected chat or channel or group.

Please get your api_hash and api_id [that you obtain through Telegram's API development tools page](https://my.telegram.org/apps) and try to connect to Telegram servers. Those api hash/id represent your application and one can be used for handling many user accounts.

Then it will attempt to sign-in (login) as a user for which you must enter the phone_number and the verification_code that will be sent to this user (for example through SMS, Email, or another Telegram client app the user is connected to).

If the verification succeeds but the phone number is unknown to Telegram, the user might be prompted to sign-up (register their account by accepting the Terms of Service) and provide their first_name and last_name.

If the account already exists and has enabled two-step verification (2FA) a password might be required.

In some case, Telegram may request that you associate an email with your account for receiving login verification codes, you may skip this step by leaving email empty, otherwise the email address will first receive an email_verification_code.

## How it works

1. Enter API Id, Hash, Phone number also FFMPEG Path for conversion -> Click on Login
2. If asks for confirmation code, enter and then click on login
3. Select the channel or chat you want to upload.
4. Click on select folder to select the needed folder
5. Click on upload to upload all the files in folder.
6. If we check the Convert to MP4 checkbox it will convert the video to mp4 to make it streamable on Telegram.
