# Service Manager

Small utility to start and manage programs.

To define the programs you want to start edit the file `appsettings.json`

```json
{
  "logPath": "./logs",
  "services": [
    {
      "name": "Grafana",
      "workingDir": "d:\\grafana-v11.4.0\\bin\\",
      "commands": [
        {
          "command": "d:\\grafana-v11.4.0\\bin\\grafana-server.exe"
        }
      ]
    },
    ...
  ]
}
```

