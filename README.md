# 🔧 Guia Completo: Compilar NBIS no Windows

Guia para compilar a biblioteca NIST NBIS no Windows e criar uma DLL para uso com C# .NET.

## 📋 Pré-requisitos

Você vai precisar instalar estas ferramentas antes de começar:

| Ferramenta | O que é | Download |
|------------|---------|----------|
| MSYS2 | Ambiente Unix-like para Windows | https://www.msys2.org/ |
| CMake | Ferramenta de build | https://cmake.org/download/ |
| Git | Controle de versão | https://git-scm.com/download/win |

---

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

---

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

---

## 📋 PASSO 3: Configurar o Build

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Criar pasta onde o NBIS será instalado
mkdir -p /c/nbis

# Configurar o build para Windows 64-bit
./setup.sh /c/nbis --MSYS --64
```

**O que esse comando faz:**
- `/c/nbis` = Pasta onde será instalado (equivale a C:\nbis no Windows)
- `--MSYS` = Indica que estamos usando MSYS/MinGW
- `--64` = Compilar para 64 bits

### 🔧 3.1 Corrigir TODOS os CMakeLists.txt

Ainda no terminal `MSYS2 MINGW64`, na pasta `nist-nbis` (raiz do repositório clonado):

```bash
find /c/projetos/nist-nbis -name "CMakeLists.txt" -exec sed -i 's/cmake_minimum_required(VERSION 2\.[0-9\.]*)/cmake_minimum_required(VERSION 3.5)/g' {} \;
```

E depois:

```bash
find /c/projetos/nist-nbis -name "CMakeLists.txt" -exec sed -i 's/CMAKE_MINIMUM_REQUIRED\s*(VERSION\s*[0-9]\.[0-9][0-9]*\.*[0-9]*)/cmake_minimum_required(VERSION 3.5)/gI' {} \;
```

### 🔧 3.2 Corrigir possível problema de variáveis globais duplicadas

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

---

## 📋 PASSO 4: Compilar o NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Gerar arquivos de configuração
make config

# Compilar (pode demorar alguns minutos)
make it
```

---

## 📋 PASSO 5: Instalar o NBIS

Ainda no terminal `MSYS2 MINGW64`:

```bash
# Instalar os binários e bibliotecas
make install LIBNBIS=yes
```

Após este comando, você terá em `C:\nbis`:

- `bin/` → Executáveis (cwsq.exe, dwsq.exe, etc.)
- `lib/` → Biblioteca estática (libnbis.a)
- `include/` → Headers (.h)

---

## 📋 PASSO 6: Corrigir a Biblioteca libnbis.a

A biblioteca `libnbis.a` gerada contém arquivos `.a` aninhados (bibliotecas dentro de bibliotecas), o que causa problemas no linking. Precisamos extrair todos os objetos `.o` e recriar a biblioteca corretamente.

```bash
cd /c/nbis/lib

# Criar pasta de trabalho
mkdir rebuild
cd rebuild

# Extrair as bibliotecas .a de dentro da libnbis.a
ar -x ../libnbis.a

# Criar pasta para todos os objetos
mkdir all_objs

# Extrair objetos de cada .a com prefixo único para evitar conflitos
for lib in *.a; do
    libname=$(basename "$lib" .a)
    echo "Processando: $lib"
    mkdir -p "temp_$libname"
    cd "temp_$libname"
    ar -x "../$lib"
    # Renomear cada .o com prefixo da biblioteca
    for obj in *.o; do
        mv "$obj" "../all_objs/${libname}_${obj}"
    done
    cd ..
    rm -rf "temp_$libname"
done

# Criar nova biblioteca com todos os objetos
ar rcs libnbis_fixed.a all_objs/*.o

# Criar índice de símbolos
ranlib libnbis_fixed.a

# Substituir a biblioteca original
cp libnbis_fixed.a ../libnbis.a

# Limpar
cd ..
rm -rf rebuild
```

---

## 📋 PASSO 7: Testar se Funcionou

```bash
# Testar o executável cwsq
/c/nbis/bin/cwsq

# Deve mostrar algo como:
# Usage: cwsq <r_bitrate> <output_ext> <image_file> [-r[awfile] w,h,d,[ppi]] [-o[utfile] outfile]
```

Se aparecer a mensagem de uso, o NBIS foi compilado com sucesso! 🎉

---

## 📋 PASSO 8: Criar a DLL para usar com C#

As bibliotecas compiladas são **estáticas** (.a). Para criar uma **DLL** que você pode usar com P/Invoke no C#, siga estes passos:

### 8.1 Criar o arquivo wrapper

Ainda no terminal MSYS2, crie uma pasta para o wrapper:

```bash
mkdir -p /c/projetos/wsq-dll
cd /c/projetos/wsq-dll
```

Crie o arquivo `wsq_nfiq_wrapper.c`:

```bash
cat > wsq_nfiq_wrapper.c << 'EOF'
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include "wsq.h"
#include "nfiq.h"

/* Variáveis globais requeridas pela libnbis */
int debug = 0;
int verbose = 0;

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT
#endif

/* ========== WSQ Functions ========== */

EXPORT int wsq_encode_wrapper(
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

EXPORT int wsq_decode_wrapper(
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

EXPORT void wsq_free(unsigned char *data)
{
    if (data != NULL) {
        free(data);
    }
}

/* ========== NFIQ Functions ========== */

EXPORT int nfiq_from_wsq_data(
    int *nfiq_score,
    unsigned char *wsq_data,
    const int wsq_len)
{
    unsigned char *raw_data = NULL;
    int w, h, d, ppi, lossy;
    int ret;
    int score;
    float conf;
    int optflag;  /* Parâmetro adicional requerido por comp_nfiq */
    
    if (wsq_data == NULL || nfiq_score == NULL) {
        return -1;
    }
    
    /* Decodificar WSQ */
    ret = wsq_decode_mem(&raw_data, &w, &h, &d, &ppi, &lossy, wsq_data, wsq_len);
    if (ret != 0) {
        return -2;
    }
    
    /* Inicializar valores */
    score = 5;
    conf = 0.0f;
    optflag = 0;
    
    /* Calcular NFIQ - assinatura correta:
       comp_nfiq(int *nfiq, float *conf, unsigned char *idata,
                 const int w, const int h, const int d, const int ppi, int *optflag) */
    ret = comp_nfiq(&score, &conf, raw_data, w, h, d, ppi, &optflag);
    
    /* Copiar resultado */
    *nfiq_score = score;
    
    /* Liberar memória */
    free(raw_data);
    
    return ret;
}

EXPORT int nfiq_from_raw_data(
    int *nfiq_score,
    unsigned char *raw_data,
    const int w,
    const int h,
    const int ppi)
{
    int ret;
    int score;
    float conf;
    int optflag;
    
    if (raw_data == NULL || nfiq_score == NULL) {
        return -1;
    }
    
    /* Inicializar valores */
    score = 5;
    conf = 0.0f;
    optflag = 0;
    
    /* Calcular NFIQ */
    ret = comp_nfiq(&score, &conf, raw_data, w, h, 8, ppi, &optflag);
    
    /* Copiar resultado */
    *nfiq_score = score;
    
    return ret;
}
EOF
```

### 8.2 Criar um header de compatibilidade sys/times.h

Pode ser que ao tentar compilar a DLL depois, ocorra erro no header sys/times.h, que é específico de Unix/Linux e não existe no MinGW Windows. Vamos contornar isso.

```
cd /c/nbis/include

# Criar a pasta sys se não existir
mkdir -p sys

# Criar um times.h vazio/stub
cat > sys/times.h << 'EOF'
/* Stub for Windows compatibility */
#ifndef _SYS_TIMES_H
#define _SYS_TIMES_H

#include <time.h>

struct tms {
    clock_t tms_utime;
    clock_t tms_stime;
    clock_t tms_cutime;
    clock_t tms_cstime;
};

static inline clock_t times(struct tms *buf) {
    if (buf) {
        buf->tms_utime = clock();
        buf->tms_stime = 0;
        buf->tms_cutime = 0;
        buf->tms_cstime = 0;
    }
    return clock();
}

#endif /* _SYS_TIMES_H */
EOF
```

Se precisar compilar a dll x86 posteriormente, copie esse header para a pasta em que estiver trabalhando na versão x86.

```
cd /c/nbis32/include
mkdir -p sys
cp /c/nbis/include/sys/times.h sys/
```

### 8.2 Compilar a DLL

```bash
gcc -shared -o wsq_nfiq_wrapper.dll wsq_nfiq_wrapper.c \
    -I/c/nbis/include \
    -L/c/nbis/lib \
    -lnbis \
    -lm \
    -static-libgcc
```

Se tudo der certo, você terá o arquivo `wsq_nfiq_wrapper.dll` em `C:\projetos\wsq-dll\`.

### 8.3 Verificar a DLL

```bash
# Verificar se a DLL foi criada
ls -la wsq_nfiq_wrapper.dll

# Verificar as funções exportadas
nm wsq_nifq_wrapper.dll | grep wsq
```

---


## 📋 PASSO 9: Se quiser também criar uma DLL x86

1) Abra o MINGW32 localizado em `C:\msys64\mingw32.exe`
2) A partir de agora, todos os comandos deverão ser feitos no terminal do MINGW32 para que a compilação x86 seja realizada

### 9.1: Instalar o toolchain x86
```
pacman -S --needed base-devel mingw-w64-i686-toolchain mingw-w64-i686-cmake git
```

### 9.2: Limpar e recombilar a NBIS

É recomendado que se faça o procedimento de forma limpa, ou seja, clone o repositório novamente e faça o processo do 0 para evitar arquivos buildados anteriormente para x64.

```
cd /c/projetos/nist-nbis

# Limpar build anterior
make clean

# Criar pasta separada para 32-bit
mkdir -p /c/nbis32

# Reconfigurar para 32-bit
./setup.sh /c/nbis32 --MSYS --32

# Aplicar correção do -fcommon no rules.mak
nano /c/projetos/nist-nbis/rules.mak
# Adicionar -fcommon no CFLAGS

# Compilar
make config
make it

# Instalar
make install LIBNBIS=yes
```

### 9.3: Corrigir a libnbis.a (32-bit)

```
cd /c/nbis32/lib

mkdir rebuild && cd rebuild

ar -x ../libnbis.a

mkdir all_objs

for lib in *.a; do libname=$(basename "$lib" .a); echo "Processando: $lib"; mkdir -p "temp_$libname"; cd "temp_$libname"; ar -x "../$lib"; for obj in *.o; do [ -e "$obj" ] && mv "$obj" "../all_objs/${libname}_${obj}"; done; cd ..; rm -rf "temp_$libname"; done

# Criar nova biblioteca
ar rcs libnbis_fixed.a all_objs/*.o

# Criar índice
ranlib libnbis_fixed.a

# Substituir original
cp libnbis_fixed.a ../libnbis.a

# Limpar
cd ..

rm -rf rebuild
```

### 9.4: Compilar a DLL 32-bit

```
cd /c/projetos/wsq-dll

gcc -shared -o wsq_wrapper_x86.dll wsq_wrapper.c \
    -I/c/nbis32/include \
    -L/c/nbis32/lib \
    -lnbis \
    -lm \
    -static-libgcc
```

--- 
## 📋 PASSO 10: Usar a DLL no C#

Copie a `wsq_nfiq_wrapper.dll` para a pasta do seu projeto C# e use este código:

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

### Exemplo de uso completo em C#:

```csharp
using System;
using System.Runtime.InteropServices;

public class WsqConverter
{
    /// <summary>
    /// Converte uma imagem raw (grayscale 8-bit) para WSQ
    /// </summary>
    /// <param name="rawImageData">Dados da imagem em grayscale 8-bit</param>
    /// <param name="width">Largura da imagem</param>
    /// <param name="height">Altura da imagem</param>
    /// <param name="ppi">Resolução em pixels por polegada (geralmente 500 para impressões digitais)</param>
    /// <param name="bitrate">Taxa de compressão (0.75 é um bom padrão para impressões digitais)</param>
    /// <returns>Dados comprimidos em formato WSQ</returns>
    public static byte[] EncodeToWsq(byte[] rawImageData, int width, int height, int ppi = 500, float bitrate = 0.75f)
    {
        IntPtr outputPtr;
        int outputLen;
        
        int result = WsqNative.wsq_encode_wrapper(
            out outputPtr,
            out outputLen,
            bitrate,
            rawImageData,
            width,
            height,
            8,  // depth = 8 bits
            ppi,
            null);
        
        if (result != 0)
            throw new Exception($"Erro ao codificar WSQ: {result}");
        
        try
        {
            byte[] wsqData = new byte[outputLen];
            Marshal.Copy(outputPtr, wsqData, 0, outputLen);
            return wsqData;
        }
        finally
        {
            WsqNative.wsq_free(outputPtr);
        }
    }

    /// <summary>
    /// Decodifica uma imagem WSQ para raw grayscale
    /// </summary>
    public static (byte[] data, int width, int height, int ppi) DecodeFromWsq(byte[] wsqData)
    {
        IntPtr outputPtr;
        int width, height, depth, ppi, lossyFlag;
        
        int result = WsqNative.wsq_decode_wrapper(
            out outputPtr,
            out width,
            out height,
            out depth,
            out ppi,
            out lossyFlag,
            wsqData,
            wsqData.Length);
        
        if (result != 0)
            throw new Exception($"Erro ao decodificar WSQ: {result}");
        
        try
        {
            byte[] rawData = new byte[width * height];
            Marshal.Copy(outputPtr, rawData, 0, rawData.Length);
            return (rawData, width, height, ppi);
        }
        finally
        {
            WsqNative.wsq_free(outputPtr);
        }
    }
}
```

---

## 🔧 Solução de Problemas

### Erro: "archive has no index; run ranlib to add one"

A biblioteca foi criada com estrutura aninhada. Execute o PASSO 6 novamente para corrigir.

### Erro: "undefined reference to `debug'"

Certifique-se de que o arquivo `wsq_wrapper.c` contém a linha:
```c
int debug = 0;
```

### Erro: "cannot find -lwsq" ou similares

O NBIS gera uma única biblioteca `libnbis.a` em vez de bibliotecas separadas. Use `-lnbis` em vez de `-lwsq -ljpegl ...`

### A DLL não carrega no C#

- Certifique-se de que a DLL está na mesma pasta do executável ou no PATH
- Use a versão 64-bit da DLL com aplicações 64-bit (e vice-versa)
- Verifique se todas as dependências estão presentes

---

## 📚 Referências

- [NIST NBIS Official](https://www.nist.gov/services-resources/software/nist-biometric-image-software-nbis)
- [WSQ Specification](https://www.fbi.gov/services/cjis/fingerprints-and-other-biometrics/biometric-specifications)
- [MSYS2 Documentation](https://www.msys2.org/docs/what-is-msys2/)

---
