﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using Arch.IS4Host.Data;
using Arch.IS4Host.Models;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace Arch.IS4Host
{
    public class Startup
    {
        public IConfiguration Configuration { get; }
        public IHostingEnvironment Environment { get; }

        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public void ConfigureServices(IServiceCollection services)
        {

            //store connectionString in a var
            var connectionString = Configuration.GetConnectionString("DefaultConnection");

            //store assembly for migrations
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            //Replace DbContext databse from Sqlite to SqlServer
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.AddMvc();

            services.Configure<IISOptions>(iis =>
            {
                iis.AuthenticationDisplayName = "Windows";
                iis.AutomaticAuthentication = false;
            });

            var builder = services.AddIdentityServer()
            

                //Use our SqlServer DB for storing configuration data
                .AddConfigurationStore(configDB => {
                    configDB.ConfigureDbContext = db => db.UseSqlServer(connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                

                //use our SqlServer for stroing opational data
                .AddOperationalStore(operationDb => {
                    operationDb.ConfigureDbContext = db => db.UseSqlServer(connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly));
                })
                .AddAspNetIdentity<ApplicationUser>();

            if (Environment.IsDevelopment())
            {
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                throw new Exception("need to configure key material");
            }

            services.AddAuthentication()
                .AddGoogle(options =>
                {
                    options.ClientId = "708996912208-9m4dkjb5hscn7cjrn5u0r4tbgkbj1fko.apps.googleusercontent.com";
                    options.ClientSecret = "wdfPY6t8H8cecgjlxud__4Gh";
                });
        }

        public void Configure(IApplicationBuilder app)
        {
            
            InitializeDatabase(app);


            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();
            app.UseIdentityServer();
            app.UseMvcWithDefaultRoute();
        }

        private void InitializeDatabase(IApplicationBuilder app){

            //using a service scope

            using( var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope() )
            {
                serviceScope.ServiceProvider.GetRequiredService<PersistedGrantDbContext>().Database.Migrate();

                var configDbContext = serviceScope.ServiceProvider.GetRequiredService<ConfigurationDbContext>();

                configDbContext.Database.Migrate();

                //create Perssted GrantDB
                if(!configDbContext.Clients.AnyAsync().Result){
                    foreach(var client in Config.GetClients()){
                        configDbContext.Clients.Add(client.ToEntity());
                    }

                    configDbContext.SaveChanges();
                }

                 if(!configDbContext.IdentityResources.AnyAsync().Result){
                    foreach(var res in Config.GetIdentityResources()){
                        configDbContext.IdentityResources.Add(res.ToEntity());
                    }

                    configDbContext.SaveChanges();
                }

                 if(!configDbContext.ApiResources.AnyAsync().Result){
                    foreach(var api in Config.GetApis()){
                        configDbContext.ApiResources.Add(api.ToEntity());
                    }

                    configDbContext.SaveChanges();
                }


            }
        }
    }
}
