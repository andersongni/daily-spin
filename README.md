# Roleta da Daily

Aplicativo desktop nativo para Windows que sorteia quem falará a seguir na daily stand-up. A interface é feita com Windows Forms e funciona sem navegador ou instalação separada do runtime .NET.

## Requisitos

- Windows 10 ou Windows 11, 64 bits.
- Para compilar o projeto: .NET SDK 8.

A publicação autocontida incluída no pacote pode ser executada sem instalar o .NET.

## Executar

Abra `RoletaDaDaily.exe`. Para compilar e publicar uma nova versão no Windows, execute na pasta do projeto:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

O executável e os arquivos necessários serão criados em `bin\Release\net8.0-windows\win-x64\publish\`. Preserve todos os arquivos dessa pasta ao distribuir o aplicativo.

O ZIP do projeto também inclui uma publicação pronta na pasta `publish`. Extraia o conteúdo e abra `publish\RoletaDaDaily.exe`.

## Usar a roleta

1. Digite ou cole os participantes no painel, um nome por linha.
2. Use **GIRAR A ROLETA** ou clique na própria roleta.
3. Na tela do resultado, escolha se deseja remover a pessoa sorteada, sortear novamente mantendo o nome, ou voltar sem removê-la.

Linhas vazias são ignoradas. Ao sair do campo de participantes, os nomes são ajustados automaticamente para maiúscula inicial em cada palavra e minúsculas nas demais letras. Nomes duplicados são identificados sem diferenciar maiúsculas de minúsculas; a primeira ocorrência é mantida após esse ajuste. A roleta pausa brevemente depois de parar antes de exibir o resultado.

O tema visual da roleta e o som de comemoração são escolhidos aleatoriamente quando o aplicativo abre. Clique no botão de cada opção para avançar para a próxima; a lista de comemorações também inclui **Desligado**. Um som mecânico de roleta, com cliques que aceleram e desaceleram, acompanha o giro. Quando o nome sorteado aparece, toca a comemoração selecionada. **Desligado** silencia os dois sons.

Use o botão **Modo claro/escuro** para alternar a aparência da interface. Essa preferência fica salva localmente. A lista de participantes não é gravada.

## Arquivos principais

- `MainForm.cs`: interface, opções cíclicas, sorteio e modo claro/escuro.
- `WheelControl.cs`: desenho da roleta e animação do giro.
- `ResultForm.cs` e `ConfettiControl.cs`: resultado e animação de confete.
- `SoundManager.cs`: som mecânico do giro, comemorações sintetizadas localmente e reprodução pela API de áudio do Windows.
- `ThemePalette.cs`: estilos de cores da roleta e da interface.
- `ParticipantService.cs`: leitura e deduplicação dos nomes.
- `UserPreferences.cs`: preferência local de aparência.
- `RoundedPanel.cs` e `RoundedButton.cs`: controles visuais com cantos arredondados.
- `Assets\RoletaDaDaily.ico`: ícone do aplicativo.
- `app.manifest`: identidade e compatibilidade do aplicativo.
