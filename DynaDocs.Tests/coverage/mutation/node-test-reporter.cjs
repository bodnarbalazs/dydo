'use strict';

// Error fields are non-enumerable; losing failureType would turn timeouts into kills.
module.exports = async function* report(events) {
  for await (const event of events) {
    yield JSON.stringify(event, (_key, value) => value instanceof Error
      ? { ...value, name: value.name, message: value.message, stack: value.stack }
      : value) + '\n';
  }
};
