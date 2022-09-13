![](https://content.gnoss.ws/imagenes/proyectos/personalizacion/7e72bf14-28b9-4beb-82f8-e32a3b49d9d3/cms/logognossazulprincipal.png)

# Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE

![](https://github.com/equipognoss/Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE/workflows/BuildSocialCacheRefresh/badge.svg)
[![Reliability Rating](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=reliability_rating)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Duplicated Lines (%)](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=duplicated_lines_density)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=vulnerabilities)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=bugs)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Security Rating](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=security_rating)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Maintainability Rating](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=sqale_rating)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=code_smells)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)
[![Technical Debt](https://sonarcloud.io/api/project_badges/measure?project=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE&metric=sqale_index)](https://sonarcloud.io/summary/new_code?id=equipognoss_Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE)

Aplicación de segundo plano que se encarga de invalidar las cachés de la bandeja de mensajes de un usuario cada vez que recibe un mensaje nuevo, para que las bandejas de mensajes estén siempre actualizadas. 

Este servicio está escuchando la cola de nombre "ColaRefrescoCacheBandejaMensajes". Se envía un mensaje a esta cola cada vez que un usuario envía un mensaje a uno o varios destinatarios desde su bandeja de mensajes de la Web, para que este servicio se encargue de invalidar la caché de la bandeja de mensajes de todos los destinatarios.

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
socialcacherefresh:
    image: gnoss/gnoss.backgroundtask.socialcacherefresh.opencore
    env_file: .env
    environment:
     virtuosoConnectionString_home: ${virtuosoConnectionString_home}
     virtuosoConnectionString: ${virtuosoConnectionString}
     acid: ${acid}
     base: ${base}
     RabbitMQ__colaServiciosWin: ${RabbitMQ}
     RabbitMQ__colaReplicacion: ${RabbitMQ}
     redis__redis__ip__master: ${redis__redis__ip__master}
     redis__redis__bd: ${redis__redis__bd}
     redis__redis__timeout: ${redis__redis__timeout}
     redis__recursos__ip__master: ${redis__recursos__ip__master}
     redis__recursos__bd: ${redis__recursos_bd}
     redis__recursos__timeout: ${redis__recursos_timeout}
     redis__liveUsuarios__ip__master: ${redis__liveUsuarios__ip__master}
     redis__liveUsuarios__bd: ${redis__liveUsuarios_bd}
     redis__liveUsuarios__timeout: ${redis__liveUsuarios_timeout}
     idiomas: "es|Español,en|English"
     tipoBD: 0
     Servicios__urlBase: "https://servicios.test.com"
     Servicios__urlFacetas: "https://servicios.test.com/facetas/CargadorFacetas"
     Servicios__urlResultados: "https://servicios.test.com/resultados/CargadorResultados"
     connectionType: "0"
     intervalo: "100"
    volumes:
     - ./logs/socialcacherefresh:/app/logs
```

Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.SemanticAIPlatform.OpenCORE

## Código de conducta
Este proyecto a adoptado el código de conducta definido por "Contributor Covenant" para definir el comportamiento esperado en las contribuciones a este proyecto. Para más información ver https://www.contributor-covenant.org/

## Licencia
Este producto es parte de la plataforma [Gnoss Semantic AI Platform Open Core](https://github.com/equipognoss/Gnoss.SemanticAIPlatform.OpenCORE), es un producto open source y está licenciado bajo GPLv3.
