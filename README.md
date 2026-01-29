# 🔧 Guia Completo: Compilar NBIS no Windows

Pré-requisitos
Você vai precisar instalar 3 ferramentas antes de começar:


| Ferramenta |	O que é |	Download |
|----------|:-------------:|------:|
| MSYS2 |	Ambiente Unix-like para Windows |	https://www.msys2.org/ |
| CMake	| Ferramenta de build |	https://cmake.org/download/ |
| Git	| Controle de versão |	https://git-scm.com/download/win |

## 📋 PASSO 1: Instalar o MSYS2

O MSYS2 é um ambiente que simula Linux no Windows, necessário porque o NBIS usa Makefiles Unix.

1) **Baixe o instalador em:** https://www.msys2.org/
  - Clique no link msys2-x86_64-xxxxxxxx.exe

2) **Execute o instalador**
  - Instale em C:\msys64 (caminho padrão)
  - **Importante:** Não use caminhos com espaços!

3) **Após instalar, abra o terminal MSYS2**
  - Vá em: Menu Iniciar → MSYS2 → MSYS2 MINGW64
  - ⚠️ Use especificamente o "MINGW64", não o "MSYS2 MSYS"!

4) **Atualize o MSYS2 (execute estes comandos no terminal que abriu):**
```bash
# Atualizar o sistema (pode pedir para fechar e reabrir)
pacman -Syu
```

Se o terminal fechar, abra novamente o `MSYS2 MINGW64` e continue:

```bash
# Continuar atualização
pacman -Su
```

5) **Instale as ferramentas de compilação:**
```bash
# Instalar GCC, Make e ferramentas necessárias
pacman -S --needed base-devel mingw-w64-x86_64-toolchain mingw-w64-x86_64-cmake git
```

Quando perguntar `Enter a selection (default=all):`, apenas pressione Enter para instalar tudo.

## 📋 PASSO 2: Baixar o Código Fonte do NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Criar uma pasta para o projeto
mkdir -p /c/projetos
cd /c/projetos

# Clonar o repositório do NBIS
git clone https://github.com/felipefranzim/nist-nbis.git

# Entrar na pasta
cd nist-nbis
```

## 📋 PASSO 3: Configurar o Build

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Criar pasta onde o NBIS será instalado
mkdir -p /c/nbis

# Configurar o build para Windows 64-bit
./setup.sh /c/nbis --MSYS --64
```

**O que esse comando faz:**
- /c/nbis = Pasta onde será instalado (equivale a C:\nbis no Windows)
- --MSYS = Indica que estamos usando MSYS/MinGW
- --64 = Compilar para 64 bits

### 🔧 Corrigir TODOS os CMakeLists.txt

Ainda no terminal `MSYS2 MINGW64`, na pasta `nist-nbis` (raiz do repositório clonado):

```bash
find /c/projetos/nist-nbis -name "CMakeLists.txt" -exec sed -i 's/cmake_minimum_required(VERSION 2\.[0-9\.]*)/cmake_minimum_required(VERSION 3.5)/g' {} \;
```

E depois:

```bash
find /c/projetos/nist-nbis -name "CMakeLists.txt" -exec sed -i 's/CMAKE_MINIMUM_REQUIRED\s*(VERSION\s*[0-9]\.[0-9][0-9]*\.*[0-9]*)/cmake_minimum_required(VERSION 3.5)/gI' {} \;
```

### 🔧 Corrigir possível problema de variáveis globais duplicadas

Abra o seguinte arquivo com nano:
```bash
nano /c/projetos/nist-nbis/rules.mak
```

Procure a linha que define `CFLAGS` (provavelmente algo como):

```bash
CFLAGS = -O2 -w -ansi ... ... ...
```

Adicione `-fcommon` no final:

```bash
CFLAGS = -O2 -w -ansi ... ... ... -fcommon
```

Para salvar no nano: `Ctrl+O` → `Enter` → `Ctrl+X`

## 📋 PASSO 4: Compilar o NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Gerar arquivos de configuração
make config

# Compilar (pode demorar alguns minutos)
make it
```

## 📋 PASSO 5: Instalar o NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Instalar os binários e bibliotecas
make install LIBNBIS=yes
```

Após este comando, você terá em `C:\nbis`:

- `bin/` → Executáveis (cwsq.exe, dwsq.exe, etc.)
- `lib/` → Bibliotecas estáticas (.a)
- `include/` → Headers (.h)

## 📋 PASSO 6: Testar se Funcionou

```bash
# Testar o executável cwsq
/c/nbis/bin/cwsq

# Deve mostrar algo como:
# Usage: cwsq <r_bitrate> <output_ext> <image_file> [-r[awfile] w,h,d,[ppi]] [-o[utfile] outfile]
```

Se aparecer a mensagem de uso, o NBIS foi compilado com sucesso! 🎉

## 📋 PASSO 7: Criar a DLL para usar com C#

As bibliotecas compiladas são **estáticas** (.a). Para criar uma **DLL** que você pode usar com P/Invoke no C#, siga estes passos:

### 7.1 Criar o arquivo wrapper

Ainda no terminal MSYS2, crie uma pasta para o wrapper:

```bash
mkdir -p /c/projetos/wsq-dll
cd /c/projetos/wsq-dll
```

Crie o arquivo `wsq_wrapper.c`:

```bash
cat > wsq_wrapper.c << 'EOF'
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "wsq.h"

#ifdef _WIN32
#define WSQ_EXPORT __declspec(dllexport)
#else
#define WSQ_EXPORT
#endif

WSQ_EXPORT int wsq_encode_wrapper(
    unsigned char **odata,
    int *olen,
    const float r_bitrate,
    unsigned char *idata,
    const int w,
    const int h,
    const int d,
    const int ppi,
    char *comment_text)
{
    return wsq_encode_mem(odata, olen, r_bitrate, idata, w, h, d, ppi, comment_text);
}

WSQ_EXPORT int wsq_decode_wrapper(
    unsigned char **odata,
    int *ow,
    int *oh,
    int *od,
    int *oppi,
    int *lossyflag,
    unsigned char *idata,
    const int ilen)
{
    return wsq_decode_mem(odata, ow, oh, od, oppi, lossyflag, idata, ilen);
}

WSQ_EXPORT void wsq_free(unsigned char *data)
{
    if (data != NULL) {
        free(data);
    }
}
EOF
```

### 7.2 Compilar a DLL

```bash
gcc -shared -o wsq_wrapper.dll wsq_wrapper.c \
    -I/c/nbis/include \
    -L/c/nbis/lib \
    -lwsq -ljpegl -lfet -lioutil -lutil -lihead -limage \
    -static-libgcc
```

Se tudo der certo, você terá o arquivo `wsq_wrapper.dll` em `C:\projetos\wsq-dll\`.

## 📋 PASSO 8: Usar a DLL no C#

Copie a `wsq_wrapper.dll` para a pasta do seu projeto C# e use este código:

```csharp
using System;
using System.Runtime.InteropServices;

namespace WsqSharp.Native
{
    public static class WsqNative
    {
        private const string DLL_NAME = "wsq_wrapper.dll";

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int wsq_encode_wrapper(
            out IntPtr odata,
            out int olen,
            float r_bitrate,
            byte[] idata,
            int w,
            int h,
            int d,
            int ppi,
            string comment_text);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern int wsq_decode_wrapper(
            out IntPtr odata,
            out int ow,
            out int oh,
            out int od,
            out int oppi,
            out int lossyflag,
            byte[] idata,
            int ilen);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        public static extern void wsq_free(IntPtr data);
    }
}
```
