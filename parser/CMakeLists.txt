cmake_minimum_required(VERSION 3.20)
project(contract_parser LANGUAGES CXX)

find_package(re2 REQUIRED)

add_library(contract_parser SHARED
    src/ClauseExtractor.cpp
    src/KeywordTrie.cpp
    src/PatternMatcher.cpp
    src/Interop.cpp
)

target_compile_features(contract_parser PUBLIC cxx_std_20)

# Hidden by default so the shared library exports only the extern "C" surface
# that P/Invoke binds to.
set_target_properties(contract_parser PROPERTIES
    CXX_EXTENSIONS OFF
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
)

# Generates PARSER_API, replacing hand-written __declspec/visibility branching.
include(GenerateExportHeader)
generate_export_header(contract_parser
    BASE_NAME PARSER
    EXPORT_MACRO_NAME PARSER_API
    EXPORT_FILE_NAME include/parser_export.h
)

target_include_directories(contract_parser PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
    ${CMAKE_CURRENT_BINARY_DIR}/include
)

# RE2 is an implementation detail and must not leak across the C-ABI boundary.
target_link_libraries(contract_parser PRIVATE re2::re2)
