<%(TOC)%>

# Database Sync Worker

Database Sync Worker is a SpatialOS server-worker, which is designed to synchronize game data between SpatialOS and an external database.

For example, a player's data might contain persistent character unlocks, stats, and customization. When this data needs to be read and modified by SpatialOS server workers, the data should be presented in the same way as all other SpatialOS data in terms of components, component updates, and commands. With Database Sync Worker, you can keep your game's logic using the same data models that are already established.

To learn more about this worker, check [README](https://github.com/spatialos/database_sync_worker) on GitHub.

<%(Callout type="info" message="If you want to use Database Sync Worker with the SpatialOS GDK for Unreal, you can follow the <%(LinkTo title="Database Sync Worker tutorial" doctype="unreal" path="/latest/content/tutorials/dbsync/tutorial-dbsync-intro")%>. The tutorial walks you through integrating this worker in the Example Project and using it to store persistent data outside of a SpatialOS deployment.")%>

<%(Nav hide="next")%>
<%(Nav hide="prev")%>

<br/>------------<br/>
_2019-07-29 Page added with limited editorial review_
[//]: # (https://improbableio.atlassian.net/browse/DOC-1151)