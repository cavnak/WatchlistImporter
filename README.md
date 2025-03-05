# WatchlistImporter

This tool takes any Letterboxd watchlist and imports it into your universal Plex watchlist.  This works even if the targeted films are not in your library yet (ex: upcoming releases).  

You will need two things: a public Letterboxd watchlist (my test account is at https://letterboxd.com/cavnak/watchlist/), and your plex token, which is the link to your universal account.  

For more detailed instructions on grabbing your token you can go to Plex's official documentation:
https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/

Current Limitations:
I have not tested it with really large lists, I bet there are issues!

Behind the scenes it uses a Plex search to attempt to match with a confidence interval, if the best match is not very good, it will skip that one.  In my list for example, there's an extraordinarily obscure film that doesn't show up on Plex at all.

It DOES handle identical titles with different release years (I tested with the 93 and 2021 versions of The Green Snake).

If it doesn't, let me know!
