/* global jest */
// Jest setup: in-memory AsyncStorage instead of the native module.
jest.mock("@react-native-async-storage/async-storage", () =>
  require("@react-native-async-storage/async-storage/jest/async-storage-mock"),
)
