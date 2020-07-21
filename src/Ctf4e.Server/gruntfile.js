module.exports = function (grunt) {
    'use strict';

    grunt.initConfig({
        pkg: grunt.file.readJSON('package.json'),

        // Copy static files
        copy: {
            main: {
                files: [
                    {expand: true, flatten: true, src: ['node_modules/open-iconic/font/css/open-iconic-bootstrap.min.css'], dest: 'wwwroot/lib/open-iconic/css/'},
                    {expand: true, flatten: true, src: ['node_modules/open-iconic/font/fonts/*'], dest: 'wwwroot/lib/open-iconic/fonts/'},
                    {expand: true, flatten: true, src: ['node_modules/jquery/dist/jquery.min.js'], dest: 'wwwroot/lib/jquery/js/'},
                    {expand: true, flatten: true, src: ['node_modules/popper.js/dist/umd/popper.min.js'], dest: 'wwwroot/lib/popper/js/'},
                    {expand: true, flatten: true, src: ['node_modules/moment/min/moment.min.js'], dest: 'wwwroot/lib/moment/js/'},
                    {expand: true, flatten: true, src: ['node_modules/moment/min/locales.min.js'], dest: 'wwwroot/lib/moment/js/'},
                    {expand: true, flatten: true, src: ['node_modules/tempusdominus-bootstrap-4/build/js/tempusdominus-bootstrap-4.min.js'], dest: 'wwwroot/lib/tempusdominus/js/'},
                    {expand: true, flatten: true, src: ['node_modules/tempusdominus-bootstrap-4/build/css/tempusdominus-bootstrap-4.min.css'], dest: 'wwwroot/lib/tempusdominus/css/'},
                    {expand: true, flatten: true, src: ['node_modules/chart.js/dist/Chart.min.js'], dest: 'wwwroot/lib/chartjs/js/'}
                ]
            }
        }
    });

    // Load task packages
    grunt.loadNpmTasks('grunt-contrib-copy');

    // Register tasks
    grunt.registerTask('default', ['copy']);
};