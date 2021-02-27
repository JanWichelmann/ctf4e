module.exports = function (grunt) {
    'use strict';

    grunt.initConfig({
        pkg: grunt.file.readJSON('package.json'),

        // Compile SASS
        sass: {
            dist: {
                options: {
                    style: 'compressed'
                },
                files: {
                    'wwwroot/css/ctf4e-server.min.css': 'design/ctf4e-server.scss'
                }
            }
        },

        // Copy static files
        copy: {
            main: {
                files: [
                    {expand: true, flatten: true, src: ['node_modules/bootstrap/dist/js/bootstrap.bundle.min.js'], dest: 'wwwroot/lib/bootstrap/js/'},
                    {expand: true, flatten: true, src: ['node_modules/chart.js/dist/Chart.min.js'], dest: 'wwwroot/lib/chartjs/js/'},
                    {expand: true, flatten: true, src: ['node_modules/@popperjs/core/dist/umd/popper.min.js'], dest: 'wwwroot/lib/popper/js/'},
                    {expand: true, flatten: true, src: ['node_modules/flatpickr/dist/flatpickr.min.js'], dest: 'wwwroot/lib/flatpickr/js/'},
                    {expand: true, flatten: true, src: ['node_modules/flatpickr/dist/l10n/de.js'], dest: 'wwwroot/lib/flatpickr/js/l10n/'},
                    {expand: true, flatten: true, src: ['node_modules/flatpickr/dist/flatpickr.min.css'], dest: 'wwwroot/lib/flatpickr/css/'},
                    {expand: true, flatten: true, src: ['node_modules/bootstrap-icons/font/bootstrap-icons.css'], dest:'wwwroot/lib/bootstrap-icons/css'},
                    {expand: true, flatten: true, src: ['node_modules/bootstrap-icons/font/fonts/bootstrap-icons.woff'], dest:'wwwroot/lib/bootstrap-icons/css/fonts'},
                    {expand: true, flatten: true, src: ['node_modules/bootstrap-icons/font/fonts/bootstrap-icons.woff2'], dest:'wwwroot/lib/bootstrap-icons/css/fonts'}
                ]
            }
        }
    });

    // Load task packages
    grunt.loadNpmTasks('grunt-contrib-copy');
    grunt.loadNpmTasks('grunt-contrib-sass');

    // Register tasks
    grunt.registerTask('default', ['sass', 'copy']);
};