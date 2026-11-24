# PosFormatacao

Instala programas básicos e atualiza os drivers recomendados pelo Windows Update após a formatação do Windows 10 ou 11.

## Como usar

1. Antes de formatar, copie **PosFormatacao.exe** para um pendrive ou outro local que você possa acessar depois. O catálogo e o motor de drivers estão incluídos no executável.
2. Após instalar o Windows, conecte o computador à internet. Se o adaptador de rede ainda precisar de um driver, instale o driver disponibilizado pelo fabricante.
3. Abra **PosFormatacao.exe** normalmente. Marque os programas nas abas e clique em **Instalar selecionados**.
4. Use **Consultar drivers** para ver os drivers recomendados ou **Atualizar drivers** para instalar. Para fazer as duas etapas em sequência, marque **Atualizar drivers após instalar os programas** antes de clicar em **Instalar selecionados**. A instalação de drivers pede autorização de administrador e abre uma janela com o resultado.
5. Se houver uma reinicialização pendente, reinicie o Windows e clique novamente em **Atualizar drivers**.

O PosFormatacao usa o WinGet para instalar programas. Se o comando estiver indisponível, instale ou atualize o **Instalador de Aplicativo** pela Microsoft Store. Os drivers disponíveis dependem do hardware e do serviço de atualização configurado no Windows. O PosFormatacao instala apenas os drivers recomendados para atualização automática. Os opcionais podem ser consultados nas configurações do Windows Update.

## Programas disponíveis

### Componentes

- VC++ 2015–2022 · x86
- VC++ 2015–2022 · x64
- VC++ 2015–2022 · ARM64
- .NET 8 Desktop Runtime
- .NET 10 Desktop Runtime
- .NET 10 SDK

### Compactação

- WinRAR
- 7-Zip

### Mídia

- VLC
- Spotify
- K-Lite Codec Pack Standard
- OBS Studio

### Navegadores

- Chrome
- Firefox
- Edge
- Brave
- Opera
- Vivaldi

### Desenvolvimento

- Python 3.14 · x64
- Python 3.14 · ARM64
- Python 3.14 · arquitetura automática
- Python Install Manager
- Git
- GitHub Desktop
- Docker Desktop
- Node.js 22
- Node.js 24
- Node.js 26 LTS
- FileZilla · download oficial
- Notepad++
- Sublime Text 4
- WinSCP
- PuTTY
- WinMerge
- Eclipse IDE for Java Developers
- Visual Studio Code
- Cursor
- R
- Arduino IDE 2

### Produtividade

- LibreOffice
- Apache OpenOffice

### Comunicação

- WhatsApp
- Discord
- Zoom

### Jogos

- Steam
- Epic Games Launcher
- GOG Galaxy

O WhatsApp é instalado pelo WinGet usando a Microsoft Store. O FileZilla abre a página oficial para instalação manual. Selecione uma linha do Node.js e uma opção do instalador Python 3.14 por execução.

## Recompilar o executável

Para alterar o catálogo da interface, edite **catalogo.tsv** e execute `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compilar.ps1`. A compilação usa o compilador do .NET Framework do Windows.
