<h1 align="center">Jellyfin HttpAuth Plugin</h1>

## About

This plugins allows users to skip the login page (and not set password in jellyfin), provided your app is configured to use a reverse proxy that provides a username through an HTTP header.

**Warning: If you allow connections to your jellyfin without a reverse proxy, or if your reverse proxy doesn't override that header, this plugin is NOT for you, as it would allow anyone to log in as any non-admin user.**

This is a fork of [UlysseM/jellyfin-plugin-httpauth](https://github.com/UlysseM/jellyfin-plugin-httpauth) (GPL v2). Differences from upstream:

- Users created by the plugin are **never administrators**, and header login is refused for existing administrators.
- New users only get access to the libraries you select in the plugin configuration page.
- The login script is injected through the [File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin instead of editing `index.html` on disk, so no Docker `chmod` workaround is needed.

## Installation

1. Modify your reverse proxy to provide a username through some HTTP Header of your choice (eg: `X-Forwarded-User`).

2. Install the **File Transformation** plugin (add `https://www.iamparadox.dev/jellyfin/plugins/manifest.json` as a plugin repository and install it from the catalog). It is required: without it, the auto-login script is not injected. If you used the upstream plugin before, you can restore the original `index.html` (or simply let Jellyfin update).

3. Add the following repository to your jellyfin repositories: `https://raw.githubusercontent.com/jtholen001/jellyfin-plugin-httpauth/gh-pages/repository.json`, install the plugin from the catalog.

4. After restarting jellyfin, head to the configuration page for the plugin, and check the box to enable the plugin. Select the libraries new users should be able to access (if you select none, new users see nothing), and choose which HTTP Header you want the app to use (default being `X-Forwarded-User`). While you're on that configuration page, pay attention to the "safety breaker" description. Having the safety-breaker on will turn the plugin off if the server ever receives a connection without authentification headers.

5. You & all your users may need to refresh your cache (Ctrl + Shift + R) to have the updated index that injects the auto-login feature.

6. (Optional) In your "branding", you can mention something like "If you see this page, try Ctrl + Shift + R to login", or you can tell them to manually log in as user: "HttpAuth", password: *whatever they want to fill*. This will work as a backup in case the script didn't get injected.

7. That's it! If you log out of your account, and gets redirected to the home page, you should be logged back in almost instantly.

## How this plugin works

- Whenever a manual login attempt is made with the user `HttpAuth`, regardless of the password, an IAuthenticationProvider implementation will log the user as whatever the http header `X-Forwarded-User` (configurable) is set to.  If that user doesn't exist, it will be automatically created as a non-admin user with access to the configured libraries only. Existing administrators are refused.
- When the server starts, the plugin registers a transformation with the File Transformation plugin, which adds a script tag to the served `index.html`. Nothing is written to disk.
- That javacript file will be served by the plugin itself does few things. On startup, it checks whether the plugin is active and if the http header is provided (by making an API call). If everything looks, it will auto-fill the login path with the right user `HttpAuth`, some random password, before logging in.
- The HTTP endpoint used to return the name of the header is intended to be call each time the page loads, and if it sees that no HTTP header are set, it will "trip a breaker", disabling the plugin. This is a safety feature, so that in case of misconfiguration, login through this endpoint are automatically disabled. This "safety breaker" can also be turned off in the configuration.

The design philosophy behind this plugin is to keep everything simple. The fact that I'm not exposing configuration to allow specific users to have specific permissions is not a missing feature; it's by design. I intend to make new release of this plugin only when necessary (eg: Jellyfin makes a breaking change to their codebase, or updates the web UI so the hook no longer works).

The very small codebase also allows for easy audits, which should reassure folks interested in giving this plugin a go. 

## Why this plugin exist

I originally started using the 9p4/jellyfin-plugin-sso. It worked great (thanks!) but it had some flaws for my usecase:
- It required setting up providers.
- There is one more button to login.
- The codebase has dependency on various libraries, making it more likely to get hit by vulnerabilities. This is aggravated by the fact that the plugin will no longer be receiving updates.

Which gets me to the point, since reverse proxy can pass in username through headers, the extra complexity can be skipped.

I also noticed there was some demand for such plugin; There was [an attempt](https://github.com/pikami/jellyfin-header-auth) at solving the same problem, but never worked and was abandoned, despite comment asking for status update.

Anyhow, I made this plugin to answer my specific need, not to gain experience in c# / .net. Based on my lack of experience in this framework, my code (file structure & comment) may appear sloppy to some, but it's good enough for me.


## I don't know much about HTTP Headers, how can I know more?

The TLDR is you can use reverse proxy like NGINX to authentificate the user through some 3rd party mechanism, and have nginx extract the username from it. You should then be able to write the username through a custom HTTP Header.

For instance, my custom setup hides jellyfin from the public internet, and I can access my jellyfin instance through:
- `mtls`, configured at the nginx level, which requires some setup and downloading certificate onto my devices. The username can then be obtained through `$ssl_client_s_dn_cn`.
- `auth_request`, using keycloak/oauth2 proxy, I'm able to capture the username through `$upstream_http_x_auth_request_preferred_username`.
- `wireguard`, my configuration assigns each user to a specific IP address on my server, and I'm using nginx `geo` to map specific IPs to username.

Lastly, you also want to make sure to block any other traffic that didn't originate from your reverse proxy, or anyone able to reach your instance without it could provide their own HTTP Header.

## Special thanks

I picked some ideas from the following jellyfin plugins: jellyfin/jellyfin-plugin-anilist, 9p4/jellyfin-plugin-sso and n00bcodr/Jellyfin-JavaScript-Injector.
