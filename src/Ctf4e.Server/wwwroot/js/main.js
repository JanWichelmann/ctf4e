function switchLanguage(element)
{
    // Set cookie
    let cookieName = document.getElementById("language-cookie-name").value;
    let selectedLanguage = "c=" + element.getAttribute("data-language-id") + "|uic=" + element.getAttribute("data-language-id");
    let expires = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toUTCString()
    document.cookie = `${cookieName}=${encodeURIComponent(selectedLanguage)}; expires=${expires}; samesite=lax; path=/`;
    location.reload();
}

// Utility function: Reads the given cookie's value.
function getCookie(name, defaultValue = undefined)
{
    // From https://stackoverflow.com/a/15724300
    const value = `; ${document.cookie}`;
    const parts = value.split(`; ${name}=`);

    let result = defaultValue;
    if(parts.length === 2)
        result = parts.pop().split(';').shift();

    return result;
}

// Utility function: Sets the given cookie's value.
function setCookie(name, value, maxAge = null)
{
    if(maxAge === null)
        document.cookie = `${name}=${value}; path=/; samesite=lax`;
    else
        document.cookie = `${name}=${value}; path=/; samesite=lax; max-age=${maxAge}`;
}

// Utility function: Retrieve a view flag from a cookie.
function getViewFlag(cookieName, flag)
{
    let cookie = getCookie(cookieName);
    if(cookie)
        return (Number(cookie) & flag) === flag;

    return false;
}

// Utility function: Toggle a view flag in a cookie.
function toggleViewFlag(cookieName, flag)
{
    let cookie = getCookie(cookieName);
    if(!cookie)
        cookie = flag;
    else
        cookie = Number(cookie) ^ flag;

    setCookie(cookieName, cookie, 3600);
}

// Sets up a view flag switch for the given cookie and flag.
function setupViewFlagSwitch(cookieName, sw, flag)
{
    sw.addEventListener("change", () => {
        toggleViewFlag(cookieName, flag);

        if(window.history.replaceState )
            window.history.replaceState(null, null, window.location.href);
        window.location = window.location.href;
    });
}
