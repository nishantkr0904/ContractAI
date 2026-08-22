# syntax=docker/dockerfile:1

# Production image for the ContractAI backend. Three stages: build the native C++
# clause engine with CMake, publish the .NET API (which stages the engine's .so next
# to the managed assembly), then assemble a lean ASP.NET runtime image.
#
# All stages use the Ubuntu 24.04 (noble) .NET images. The clause engine calls RE2's
# std::string_view Match() overload, which needs RE2 >= 2023 (noble's libre2.so.10);
# Debian bookworm's older RE2 keeps a distinct re2::StringPiece and will not compile
# the parser. abseil is a build-time need only — re2.h includes its headers — while
# noble's libre2 runtime package links no abseil, so the runtime image stays lean.

# ---- Stage 1: native clause engine (CMake / C++20) --------------------------------
# Built on the .NET SDK's noble base so libcontract_parser.so links against the same
# glibc/libstdc++/RE2 ABI the runtime stage ships — that is what lets P/Invoke load it
# there without surprises.
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS parser-build

# build-essential: CMake's Unix Makefiles generator needs a compiler and make.
# libre2-dev: RE2 headers + shared library. libabsl-dev: re2.h includes abseil headers.
# (Both are build-only and never reach the runtime image.)
RUN apt-get update \
    && apt-get install -y --no-install-recommends build-essential cmake libre2-dev libabsl-dev \
    && rm -rf /var/lib/apt/lists/*

# Debian/Ubuntu's libre2-dev ships a pkg-config file but no CMake package config, so
# parser/CMakeLists.txt's find_package(re2) can't locate it. Supply a minimal config
# that adapts the installed library into the re2::re2 target the parser links against;
# find_* keep it architecture-independent.
RUN mkdir -p /opt/re2-cmake && cat > /opt/re2-cmake/re2Config.cmake <<'EOF'
find_path(RE2_INCLUDE_DIR NAMES re2/re2.h)
find_library(RE2_LIBRARY NAMES re2)
if(NOT RE2_INCLUDE_DIR OR NOT RE2_LIBRARY)
  message(FATAL_ERROR "re2 shim: libre2-dev not found")
endif()
add_library(re2::re2 SHARED IMPORTED)
set_target_properties(re2::re2 PROPERTIES
  IMPORTED_LOCATION "${RE2_LIBRARY}"
  INTERFACE_INCLUDE_DIRECTORIES "${RE2_INCLUDE_DIR}")
EOF

WORKDIR /src
COPY parser/ parser/

# BUILD_TESTING=OFF: production needs the shared library, not the CTest binary.
RUN cmake -S parser -B parser/build -DCMAKE_BUILD_TYPE=Release -DBUILD_TESTING=OFF -Dre2_DIR=/opt/re2-cmake \
    && cmake --build parser/build

# ---- Stage 2: .NET publish --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0-noble AS publish
WORKDIR /src

# Restore against the project files alone so this layer is reused when only source
# changes. Publishing the API pulls in just its graph (Core/Services/Data), not Tests.
COPY backend/ContractAI.Core/ContractAI.Core.csproj         backend/ContractAI.Core/
COPY backend/ContractAI.Data/ContractAI.Data.csproj         backend/ContractAI.Data/
COPY backend/ContractAI.Services/ContractAI.Services.csproj backend/ContractAI.Services/
COPY backend/ContractAI.API/ContractAI.API.csproj           backend/ContractAI.API/
RUN dotnet restore backend/ContractAI.API/ContractAI.API.csproj

COPY backend/ backend/

# The API .csproj copies parser/build/libcontract_parser.* next to the assembly
# (guarded on the directory existing). Stage the Linux .so from stage 1 so publish
# carries it into the output — this is the file P/Invoke resolves at runtime.
COPY --from=parser-build /src/parser/build/libcontract_parser.so parser/build/

RUN dotnet publish backend/ContractAI.API/ContractAI.API.csproj \
    -c Release --no-restore -o /app/publish

# ---- Stage 3: runtime -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble AS runtime

# libre2-10 is the engine's only extra runtime dependency; its other needs (libc6,
# libgcc-s1, libstdc++6) already ship in the base image. noble's libre2 links no abseil,
# so this adds well under a megabyte and the runtime image stays lean.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libre2-10 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=publish /app/publish ./

# Run as the image's pre-provisioned non-root user.
USER $APP_UID

# Kestrel listens on 8080 in the Microsoft images (ASPNETCORE_HTTP_PORTS=8080).
EXPOSE 8080

ENTRYPOINT ["dotnet", "ContractAI.API.dll"]
