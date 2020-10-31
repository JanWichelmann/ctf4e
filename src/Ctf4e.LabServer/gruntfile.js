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
                    'wwwroot/css/ctf4e-labserver.min.css': 'design/ctf4e-labserver.scss'
                }
            }
        },

        // Copy static files
        copy: {
            main: {
                files: [
                    {expand: true, flatten: true, src: ['node_modules/bootstrap/dist/js/bootstrap.bundle.min.js'], dest: 'wwwroot/lib/bootstrap/js/'},
                    {expand: true, flatten: true, src: ['node_modules/open-iconic/font/css/open-iconic-bootstrap.min.css'], dest: 'wwwroot/lib/open-iconic/css/'},
                    {expand: true, flatten: true, src: ['node_modules/open-iconic/font/fonts/*'], dest: 'wwwroot/lib/open-iconic/fonts/'},
                    {expand: true, flatten: true, src: ['node_modules/jquery/dist/jquery.min.js'], dest: 'wwwroot/lib/jquery/js/'}
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