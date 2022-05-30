# Gnoss.BackgroundTask.SocialCacheRefresh.OpenCORE

Aplicación de segundo plano que se encarga de invalidar las cachés de la bandeja de mensajes de un usuario cada vez que recibe un mensaje nuevo, para que las bandejas de mensajes estén siempre actualizadas. 

Configuración estandar de esta aplicación en el archivo docker-compose.yml: 

```yml
socialcacherefresh:
    image: socialcacherefresh
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

Se pueden consultar los posibles valores de configuración de cada parámetro aquí: https://github.com/equipognoss/Gnoss.Platform.Deploy
