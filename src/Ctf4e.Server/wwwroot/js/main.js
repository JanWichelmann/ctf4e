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

// Utility function: Delay callback to after n milliseconds.
function delay(callback, n)
{
    var delayTimer = 0;
    return function()
    {
        clearTimeout(delayTimer); // Any action resets the timer
        delayTimer = setTimeout(callback, n);
    };
}

// Utility function: AJAX GET with callback.
function ajaxGet(url, callback, spinner = null)
{
    // Prepare request
    let xhr = new XMLHttpRequest();
    xhr.open("GET", url);
    xhr.onload = () =>
    {
        if(spinner !== null)
            spinner.classList.add("invisible");
        if(callback !== null)
            callback(xhr.status, xhr.responseText);
    };

    // Run request
    if(spinner !== null)
        spinner.classList.remove("invisible");
    xhr.send();
}

// Utility function: AJAX POST with callback.
function ajaxPost(url, data, callback, useFormData = false, spinner = null)
{
    // Prepare request
    let xhr = new XMLHttpRequest();
    xhr.open("POST", url);
    xhr.setRequestHeader("RequestVerificationToken", document.getElementById("csrf-token").value);
    xhr.onload = () =>
    {
        if(spinner !== null)
            spinner.classList.add("invisible");
        if(callback !== null)
            callback(xhr.status, xhr.responseText);
    };

    // Run request
    if(spinner !== null)
        spinner.classList.remove("invisible");
    
    if(useFormData)
    {
        let formData = new FormData();
        for(let key in data)
            formData.append(key, data[key]);
        xhr.send(formData);
    }
    else
    {
        xhr.setRequestHeader("Content-Type", "application/json");
        xhr.send(JSON.stringify(data));
    }
}