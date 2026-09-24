cmake_minimum_required(VERSION 3.24)
project(PocketMicVirtualMic LANGUAGES CXX)

include(FetchContent)
set(FETCHCONTENT_QUIET OFF)
FetchContent_Declare(libASPL
  GIT_REPOSITORY https://github.com/gavv/libASPL.git
  GIT_TAG 47f688ed6bb637ab8b7f4b36864734b2b1f69b6b
)
FetchContent_MakeAvailable(libASPL)

set(DRIVER_NAME PocketMicVirtualMic)
set(DRIVER_VERSION 0.1.6)
set(DRIVER_UID 21745612-6791-45CD-9B31-3D1CD157FA66)
set(DRIVER_ENTRYPOINT PocketMicVirtualMicEntryPoint)
set(DRIVER_IDENTIFIER com.canopydigital.pocketmic.virtual-mic)
set(DRIVER_COPYRIGHT "Copyright (c) Canopy Digital")

configure_file(Info.plist.in Info.plist @ONLY)

add_library(${DRIVER_NAME} MODULE Driver.cpp)
target_compile_features(${DRIVER_NAME} PRIVATE cxx_std_17)
target_link_libraries(${DRIVER_NAME} PRIVATE libASPL)
find_library(COREFOUNDATION_FRAMEWORK CoreFoundation REQUIRED)
find_library(COREAUDIO_FRAMEWORK CoreAudio REQUIRED)
target_link_libraries(${DRIVER_NAME} PRIVATE ${COREFOUNDATION_FRAMEWORK} ${COREAUDIO_FRAMEWORK})
set_target_properties(${DRIVER_NAME} PROPERTIES
  PREFIX ""
  SUFFIX ""
  BUNDLE TRUE
  BUNDLE_EXTENSION driver
  MACOSX_BUNDLE TRUE
  MACOSX_BUNDLE_INFO_PLIST "${CMAKE_CURRENT_BINARY_DIR}/Info.plist"
  MACOSX_BUNDLE_BUNDLE_NAME "PocketMicVirtualMic"
  MACOSX_BUNDLE_BUNDLE_VERSION "${DRIVER_VERSION}"
  MACOSX_BUNDLE_GUI_IDENTIFIER "${DRIVER_IDENTIFIER}"
  MACOSX_BUNDLE_SHORT_VERSION_STRING "${DRIVER_VERSION}"
)
