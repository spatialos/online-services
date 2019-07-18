
# Local Metagame Services: Docker volumes on Windows
<%(TOC)%>
> This guide is for Windows users only.

If you are running Metagame Services locally on Windows, you may need extra steps using Docker volumes.

Most Docker functionality works as you'd expect on Windows. However, getting volume mounting to work properly can take additional steps on some systems.

You can test whether volumes are working by running this command:

    ```bash
    docker run --rm -v c:/Users/yourname:/data alpine ls /data
    ```

If you see a list of the files in your home directory, volumes are already working correctly; you don't need to do anything.

If you get "Permission denied", or some other error, try following these steps. This guide assumes you're on Windows 10.

1. Open the **Computer Management** application and navigate to the **Local Users and Groups** section.

2. Create a new user. Call it something like `DockerHost`, and give it a password you'll remember. Uncheck the option **User must change password at next logon**.

    ![]({{assetRoot}}img/docker-windows-user.png)

3. Make this user an Administrator by right-clicking on the user and selecting **Properties**. Navigate to the **Member Of** tab and add the `Administrators` group.

4. Open the Docker Desktop settings - right-click on the icon in your system tray and click on **Settings** - and navigate to the **Shared Drives** tab. Turn on sharing for the drive your files are on (probably C), and authenticate using the credentials for your new Docker user.

5. Finally, you need to give the new Docker user access to your user's home directory (`C:\Users\yourname`). You can do this from Explorer by right-clicking on the directory, clicking **Properties**, navigating to the **Sharing** tab and clicking the share button.

6. You should now be able to run the command successfully:
    ```bash
    docker run --rm -v c:/Users/yourname:/data alpine ls /data
    ```
<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-16 Page added with limited editorial review_
[//]: # (TODO: https://improbableio.atlassian.net/browse/DOC-1135)