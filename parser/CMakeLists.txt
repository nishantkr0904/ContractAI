cmake_minimum_required(VERSION 3.20)
project(contract_parser LANGUAGES CXX)

find_package(re2 REQUIRED)

set(PARSER_SOURCES
    src/ClauseExtractor.cpp
    src/KeywordTrie.cpp
    src/PatternMatcher.cpp
    src/Interop.cpp
)

add_library(contract_parser SHARED ${PARSER_SOURCES})

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

# Off in the production image build (task 6.9), which needs no test binary.
option(BUILD_TESTING "Build the parser test suite" ON)

if(BUILD_TESTING)
    enable_testing()

    # Compiles the sources rather than linking contract_parser: the library hides
    # every symbol but the extern "C" exports, and the suite tests the internal
    # contractai:: API too. PARSER_STATIC_DEFINE neutralizes PARSER_API, which
    # would otherwise resolve to dllimport for a consumer of the shared library.
    add_executable(test_extraction tests/test_extraction.cpp ${PARSER_SOURCES})
    target_compile_features(test_extraction PRIVATE cxx_std_20)
    target_compile_definitions(test_extraction PRIVATE PARSER_STATIC_DEFINE)
    target_include_directories(test_extraction PRIVATE
        ${CMAKE_CURRENT_SOURCE_DIR}/include
        ${CMAKE_CURRENT_BINARY_DIR}/include
    )
    target_link_libraries(test_extraction PRIVATE re2::re2)

    # Opt-in for the ASan check (task 2.10) and CI in Phase 6; off by default so
    # normal and production builds are untouched. UBSan rides along. macOS has no
    # LeakSanitizer, so leak verdicts come from `leaks`; ASan here still catches
    # use-after-free, double-free, and alloc/dealloc mismatch.
    option(ENABLE_ASAN "Build the test suite with AddressSanitizer + UBSan" OFF)
    if(ENABLE_ASAN)
        target_compile_options(test_extraction PRIVATE
            -fsanitize=address,undefined -fno-omit-frame-pointer -g)
        target_link_options(test_extraction PRIVATE -fsanitize=address,undefined)
    endif()

    # One test per suite so a failure names the area, not just the binary.
    foreach(suite tokenize trie patterns extract abi)
        add_test(NAME parser.${suite} COMMAND test_extraction ${suite})
    endforeach()
endif()
