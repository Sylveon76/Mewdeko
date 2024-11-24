# Creating the Bot
**\1. Go to [Discord Developer Portal](https://discord.dev) and login.**
**\2. Click on `new application`.**
**\3. Name the application and click create.**
**\4. Navigate to the `Bot` tab on the siderbar.**
**\5. Click reset token input 2FA code and copy the token for later.**
# Download
**\1. Run this following command in a terminal (Debian/Ubuntu)**
```
sudo apt update && sudo apt install postgres postgres-contrib dotnet-sdk-8.0 git
```
**\2. Download Mewdeko from the [releases page](https://github.com/SylveonDeko/Mewdeko/releases) and download the source code zip/tar.gz (not the win64 binary).**
**\3. Unzip the file with either the `tar` command or `unzip` command.**
**\4. Move the folder you just unzipped to a different directory such as say /srv.**
# Database setup
**1. Switch to the postgres user**
   ```
   sudo -i -u postgres
   ```
**2. Create a new PostgreSQL role**
   ```
   createuser --interactive
   ```
**3. Create a new database**
   ```
   createdb <dbName>
   ```
**4. Set password for the newly created role.**
   ```
   psql
     ALTER USER <username> WITH ENCRYPTED PASSWORD '<password>';
     \q
   ```
**5. Edit PostgreSQL configuration to allow password authenication.**
   - Open the configuration file.
    ```
    sudo nano /etc/postgresql/<yourVersion>/main/pg_hba.conf
    ```
   - Find the lines that look like this and change `peer` to `md5`:
   ```
   local   all             postgres                                peer
     local   all             all                                     peer
     host    all             all             127.0.0.1/32            md5
     host    all             all             ::1/128                 md5
   ```
   - Restart PostgreSQL
   ```
   sudo systemctl restart postgresql
   ```
**6. Set up the PostgreSQL connection string in `credentials.json`:**
   Format: `"PsqlConnectionString": "Server=ServerIp;Database=DatabaseName;Port=PsqlPort;UID=PsqlUser;Password=UserPassword"`
# Setup `credentials.json`.
**Follow the [credentials guide](https://mewdeko.tech/credguide)**
# Final Setup
**1. To run the bot do the command `dotnet run -c release` in terminal while inside the directory where the .csproj file (e.g., `/srv/Mewdeko/src/Mewdeko`) to shutdown the bot do .die or Ctrl+C in the terminal window optionally you can make it into a systemd service.**
**2. Go to [Discord Developer Portal](https://discord.dev) and login.**
**3. Find the application you just created.**
**4. Navigate to the `OAuth2` tab.**
**5. Go to the `OAuth2 URL Generator` and select the scopes `bot` and `application.commands`. The URL should look like this: `https://discord.com/oauth2/authorize?client_id=<clientID>&permissions=0&integration_type=0&scope=applications.commands+bot`.**
**5. Copy the URL and paste it into the browser and invite the bot to your server.**
Optional systemd service example:
```
[Unit]
Description=Mewdeko
After=network.target

[Service]
WorkingDirectory=/srv/Mewdeko/src/Mewdeko
ExecStart=/usr/bin/dotnet run -c release
ExecStop=/bin/bash -c "kill -SIGINT 3687"
Restart=on-failure
Killsignal=SIGINT
TimeoutStopSec=10
User=<yourUsername>
Group=<sameAsUsername>
Environment=ASPNETCORE_ENVIRONMENT=Production

# Optionally start on boot
[Install]
WantedBy=multi-user.target
```
