function switchLanguage(element)
{
    // Set cookie
    let cookieName = document.getElementById("language-cookie-name").value;
    let selectedLanguage = "c=" + element.getAttribute("data-language-id") + "|uic=" + element.getAttribute("data-language-id");
    let expires = new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toUTCString()
    document.cookie = `${cookieName}=${encodeURIComponent(selectedLanguage)}; expires=${expires}; samesite=lax; path=/`;
    location.reload();
}
