# Creating the Bot
**1. Go to [Discord Developer Portal](https://discord.dev) and login.**
**2. Click on `new application`.**
**3. Name the application and click create.**
**4. Navigate to the `Bot` tab on the siderbar.**
**5. Click reset token input 2FA code and copy the token for later.**

**Run this following command in a terminal (Debian/Ubuntu)**
```
sudo apt update && sudo apt install postgres postgres-contrib dotnet-sdk-8.0 git
```
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
Follow the [credentials guide](https://mewdeko.tech/credguide)
