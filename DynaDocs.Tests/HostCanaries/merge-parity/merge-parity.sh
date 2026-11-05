#!/usr/bin/env bash
# DYD-426 merge parity harness: throwaway probe PRs into feature/probe, and a read-only check of origin.
# The rows themselves are never run here; a host session issues each as its own shell call (README.md).
set -euo pipefail

readonly REPO=BalazsOrg/dydo
readonly PROBE=feature/probe
readonly SLOTS=(codex-main codex-worktree claude-main claude-worktree sentinel)

usage() {
    cat >&2 <<'EOF'
usage: merge-parity.sh setup  --run <id> --base <feature sha40> --out <dir>
       merge-parity.sh verify --run <id> --ran <slot,slot,...|"">
slots: codex-main codex-worktree claude-main claude-worktree sentinel
EOF
    exit 2
}

die() { echo "merge-parity: $*" >&2; exit 1; }

branch_of() { echo "probe/DYD-426-$1-$2"; }

# A rerun under the same id would open duplicate PRs, so the id is single-use.
check_run_id() {
    [[ $1 =~ ^[A-Za-z0-9][A-Za-z0-9._-]*$ ]] || die "--run must be letters, digits, '.', '_' or '-'"
}

pr_body() { # slot head tip
    local verdict="PASS - rubric/type: code; probe fixture for the DYD-426 merge parity matrix, no change under review, throwaway PR into $PROBE; candidate $2; base $3"
    cat <<EOF
DYD-426 merge parity probe: slot \`$1\`.

**This is a fixture, not a review.** It adds one throwaway file to \`$PROBE\` so a host session can
run the DYD-426 command matrix against it. Nobody reviewed anything; the line under
\`## Independent review\` exists only because \`dydo merge\` refuses a PR without one. Never merge
this PR by hand. $(if [[ $1 == sentinel ]]; then echo "The sentinel is never merged: row 3 tries a raw \`gh pr merge\` on it, and the guard must block that."; else echo "Only the \`$1\` host session merges it, through \`dydo merge\` (rows 1-2)."; fi)

## Independent review

$verdict
EOF
}

setup() {
    local run="" base="" out=""
    while (($#)); do
        case $1 in
            --run) run=${2-}; shift 2 ;;
            --base) base=${2-}; shift 2 ;;
            --out) out=${2-}; shift 2 ;;
            *) usage ;;
        esac
    done
    [[ -n $run && -n $base && -n $out ]] || usage
    check_run_id "$run"
    [[ $base =~ ^[0-9a-f]{40}$ ]] || die "--base takes a full 40-character lowercase SHA"

    local origin
    origin=$(git -C "$(dirname "$0")" remote get-url origin)
    for slot in "${SLOTS[@]}"; do
        [[ -z $(git ls-remote "$origin" "refs/heads/$(branch_of "$run" "$slot")") ]] \
            || die "$(branch_of "$run" "$slot") already exists on origin; choose a new --run id"
    done

    # feature/probe is reused at whatever tip it has; it is created only when absent.
    local tip created=false
    tip=$(git ls-remote "$origin" "refs/heads/$PROBE" | cut -f1)
    if [[ -z $tip ]]; then
        git -C "$(dirname "$0")" cat-file -e "$base^{commit}" || die "base $base is not in this checkout; fetch it first"
        git -C "$(dirname "$0")" push "$origin" "$base:refs/heads/$PROBE"
        tip=$base
        created=true
    fi
    echo "$PROBE tip: $tip (created: $created)"

    # pr-ready demands a clean tree whose HEAD equals its upstream, so the probe commits are made in a
    # throwaway clone, never in the caller's checkout. clone stays global: the EXIT trap runs after
    # setup returns, when a local would be gone.
    mkdir -p "$out"
    out=$(cd "$out" && pwd)
    clone=$(mktemp -d "${TMPDIR:-/tmp}/merge-parity-$run.XXXXXX")
    trap 'rm -rf "$clone"' EXIT
    git clone --quiet --no-checkout "$(git -C "$(dirname "$0")" rev-parse --show-toplevel)" "$clone"
    git -C "$clone" remote set-url origin "$origin"
    git -C "$clone" fetch --quiet origin "refs/heads/$PROBE:refs/remotes/origin/$PROBE"

    local slots_json="{}"
    for slot in "${SLOTS[@]}"; do
        local branch head url body="$out/$slot.body.md"
        branch=$(branch_of "$run" "$slot")
        git -C "$clone" switch --quiet --create "$branch" "origin/$PROBE"
        mkdir -p "$clone/probe"
        echo "DYD-426 merge parity probe $run, slot $slot. Throwaway; delete with $PROBE." > "$clone/probe/$run-$slot.txt"
        git -C "$clone" add "probe/$run-$slot.txt"
        git -C "$clone" commit --quiet -m "DYD-426 probe: $run $slot fixture"
        git -C "$clone" push --quiet --set-upstream origin "$branch"
        head=$(git -C "$clone" rev-parse HEAD)
        pr_body "$slot" "$head" "$tip" > "$body"
        dydo pr-ready "$clone" --body "$body"
        url=$(gh pr create --repo "$REPO" --base "$PROBE" --head "$branch" \
            --title "DYD-426 probe $run $slot (fixture; never merge by hand)" --body-file "$body")
        echo "$slot: PR ${url##*/} $url head $head"
        slots_json=$(jq --arg slot "$slot" --arg branch "$branch" --argjson pr "${url##*/}" --arg url "$url" --arg head "$head" \
            '.[$slot] = {branch: $branch, pr: $pr, url: $url, head: $head}' <<< "$slots_json")
    done

    jq -n --arg run "$run" --arg repo "$REPO" --arg base "$base" --arg tip "$tip" --argjson created "$created" \
        --argjson slots "$slots_json" \
        '{run: $run, repo: $repo, base: $base, featureProbe: {tip: $tip, created: $created}, slots: $slots}' \
        | tee "$out/setup.json"
}

verify() {
    local run="" ran="" have_ran=false
    while (($#)); do
        case $1 in
            --run) run=${2-}; shift 2 ;;
            --ran) ran=${2-}; have_ran=true; shift 2 ;;
            *) usage ;;
        esac
    done
    if [[ -z $run ]] || ! $have_ran; then usage; fi
    check_run_id "$run"
    for slot in ${ran//,/ }; do
        [[ " ${SLOTS[*]} " == *" $slot "* && $slot != sentinel ]] || die "--ran takes host slots only, not '$slot'"
    done

    local ok=true
    for slot in "${SLOTS[@]}"; do
        local branch pull number state merged merge="" trailer=no expect
        branch=$(branch_of "$run" "$slot")
        pull=$(gh api "repos/$REPO/pulls?head=${REPO%%/*}:$branch&state=all" --jq 'if length == 1 then .[0] else empty end')
        if [[ -z $pull ]]; then
            echo "$slot: no single PR from $branch  [FAIL]"
            ok=false
            continue
        fi
        number=$(jq -r .number <<< "$pull")
        state=$(jq -r .state <<< "$pull")
        merged=$(jq -r .merged_at <<< "$pull")
        if [[ $merged != null ]]; then
            merged=yes
            merge=$(jq -r .merge_commit_sha <<< "$pull")
            [[ $'\n'$(gh api "repos/$REPO/git/commits/$merge" --jq .message) == *$'\nDydo-Merge: '* ]] && trailer=yes
        else
            merged=no
        fi

        if [[ ",$ran," == *",$slot,"* ]]; then
            expect="merged with Dydo-Merge"
            [[ $merged == yes && $trailer == yes ]] || expect="$expect  [FAIL]"
        else
            expect="open and unmerged"
            [[ $state == open && $merged == no ]] || expect="$expect  [FAIL]"
        fi
        [[ $expect == *FAIL* ]] && ok=false
        echo "$slot: PR #$number state=$state merged=$merged merge=${merge:-none} trailer=$trailer; expected $expect"
    done

    local main default
    main=$(gh api "repos/$REPO/git/matching-refs/heads/main" --jq '[.[] | select(.ref == "refs/heads/main")] | length')
    default=$(gh api "repos/$REPO" --jq .default_branch)
    echo "origin refs/heads/main: $([[ $main == 0 ]] && echo absent || echo "PRESENT  [FAIL]")"
    echo "default branch: $default$([[ $default == master ]] || echo "  [FAIL]")"
    [[ $main == 0 && $default == master ]] || ok=false

    if ! $ok; then
        echo "verify: MISMATCH"
        exit 1
    fi
    echo "verify: matches the expected state"
}

case ${1-} in
    setup) shift; setup "$@" ;;
    verify) shift; verify "$@" ;;
    *) usage ;;
esac
