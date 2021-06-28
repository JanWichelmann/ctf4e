# Design customization

The CSS theme is based on [Bootstrap](https://getbootstrap.com/), and can be customized by modifying the respective SCSS files in the design/ subfolders, and overriding the resulting CSS files in the Docker containers.

## Example

1. Copy `package.json`, `gruntfile.js` and the `design/` folder into a working directory.
1. Change the `design/ctf4e-server.scss` file as desired, e.g. by setting a new primary color:
    
    ```scss
    $blue: #0000ff; // More blue
    ```
1. Run `npm install` to download dependencies.
1. Run `npx grunt` to compile the SCSS.
1. Mount the generated `wwwroot/css/ctf4e-server.min.css` into the Docker container:
    
    ```yaml
    volumes:
      # ...
      - type: bind
        source: working/directory/wwwroot/css/ctf4e-server.min.css
        target: /app/wwwroot/css/ctf4e-server.min.css
    ```
