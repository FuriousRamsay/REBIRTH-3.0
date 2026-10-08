// The current fixture extracts both unchanged legacy methods and opt-in owned methods.
// Its first26 assertions preserve the original default-input baseline.
console.log('Default-input baseline is included in the maintained owned-input fixture.');
await import('./test_poi_input_owned.mjs');
